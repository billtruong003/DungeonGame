using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Unity.Profiling;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System;
using System.Linq;

namespace CleanRender
{
    /// <summary>
    /// Performance Benchmark Tool v2.1 — Unity 6 Compatible (Ultra Low Overhead)
    /// 
    /// Accurate performance measurement using Unity ProfilerRecorder API.
    /// Captures: FPS, Frame Time, CPU/GPU Time, Draw Calls, Batches,
    /// SetPass Calls, Triangles, Vertices, Shadow Casters, GC Alloc,
    /// Memory, Stutter Analysis, Frame Time Distribution.
    ///
    /// v2.1 — Near-zero overhead design:
    ///   - GPU timing OFF by default (FrameTimingManager forces GPU sync = huge perf hit)
    ///   - No OnGUI during recording by default (IMGUI = multiple calls/frame + GC alloc)
    ///   - Recorders only active during benchmark, zero cost when idle
    ///   - Pre-allocated array, no List, no LINQ during recording
    ///   - VSync auto-disabled during benchmark, restored after
    ///
    /// Usage:
    ///   Gắn vào GameObject → Play Mode → Inspector bấm Start
    ///   Hoặc dùng Editor Window: Tools → CleanRender → Benchmark
    ///
    /// Hotkeys:
    ///   F8  = Start/Stop benchmark
    ///   F9  = Quick 5s benchmark
    /// </summary>
    [AddComponentMenu("CleanRender/Performance Benchmark v2")]
    public class PerformanceBenchmark : MonoBehaviour
    {

        [Header("━━━ Benchmark Settings ━━━")]
        [Tooltip("Thời gian benchmark (giây)")]
        public float benchmarkDuration = 10f;

        [Tooltip("Bỏ qua N frame đầu (warmup để GC/shader compile ổn định)")]
        public int warmupFrames = 60;

        [Tooltip("Tên config hiện tại (ghi vào report)")]
        public string configLabel = "Default";

        [Tooltip("Đường dẫn xuất report (relative to project root)")]
        public string outputFolder = "BenchmarkReports";

        [Header("━━━ GPU Timing ━━━")]
        [Tooltip("BẬT = đo GPU chính xác nhưng TỤT ~50-100 FPS (FrameTimingManager force GPU sync mỗi frame)\n" +
                 "TẮT = không tụt FPS, GPU timing sẽ hiện N/A trong report")]
        public bool enableGpuTiming = false;

        [Header("━━━ Overlay ━━━")]
        [Tooltip("Hiện overlay HUD khi benchmark.\n" +
                 "OnGUI gọi 2+ lần/frame + tạo GC alloc → tắt để đo chính xác hơn")]
        public bool showOverlay = false;

        [Header("━━━ Camera Path (Optional) ━━━")]
        [Tooltip("Camera tự di chuyển theo path để benchmark consistent")]
        public Transform[] cameraPath;
        public float pathSpeed = 5f;

        [Header("━━━ Runtime State (Read Only) ━━━")]
        [SerializeField] private bool _isBenchmarking;
        [SerializeField] private bool _isWarmingUp;
        [SerializeField] private int _warmupRemaining;
        [SerializeField] private float _progress;
        [SerializeField] private float _currentFPS;
        [SerializeField] private int _recordedFrames;

        public bool IsBenchmarking => _isBenchmarking;
        public bool IsWarmingUp => _isWarmingUp;
        public float Progress => _progress;
        public float CurrentFPS => _currentFPS;
        public int RecordedFrames => _recordedFrames;
        public bool GpuTimingEnabled => enableGpuTiming;
        public BenchmarkResult LastResult { get; private set; }

        // --- Recorders (only active during benchmark) ---
        private ProfilerRecorder _drawCallsRecorder;
        private ProfilerRecorder _batchesRecorder;
        private ProfilerRecorder _setPassRecorder;
        private ProfilerRecorder _trianglesRecorder;
        private ProfilerRecorder _verticesRecorder;
        private ProfilerRecorder _shadowCastersRecorder;
        private ProfilerRecorder _gcAllocRecorder;
        private ProfilerRecorder _mainThreadRecorder;
        private ProfilerRecorder _renderThreadRecorder;
        private ProfilerRecorder _totalMemoryRecorder;
        private ProfilerRecorder _gcMemoryRecorder;
        private ProfilerRecorder _gfxMemoryRecorder;
        private ProfilerRecorder _meshMemoryRecorder;
        private ProfilerRecorder _textureMemoryRecorder;
        private ProfilerRecorder _visibleSkinnedMeshesRecorder;
        private ProfilerRecorder _renderTexturesCountRecorder;
        private ProfilerRecorder _renderTexturesBytesRecorder;
        private ProfilerRecorder _usedTexturesCountRecorder;
        private ProfilerRecorder _usedTexturesBytesRecorder;
        private bool _recordersActive;

        private FrameTiming[] _frameTimings = new FrameTiming[1];

        private struct FrameData
        {
            public float deltaTime;
            public float cpuMainMs;
            public float cpuRenderMs;
            public float gpuMs;
            public int drawCalls;
            public int batches;
            public int setPassCalls;
            public long triangles;
            public long vertices;
            public int shadowCasters;
            public long gcAllocBytes;
            public int visibleSkinnedMeshes;
        }

        // Pre-allocated frame buffer — zero GC during recording
        private FrameData[] _frameBuffer;
        private int _frameCount;

        private float _benchmarkStartTime;
        private int _cameraPathIndex;
        private float _cameraPathT;

        // Saved render settings
        private int _savedVSyncCount;
        private int _savedTargetFrameRate;
        private bool _settingsSaved;

        private BenchmarkResult _beforeResult;

        public static event Action<PerformanceBenchmark> OnBenchmarkComplete;

        private void OnDisable()
        {
            if (_isBenchmarking) StopBenchmark();
        }

        private void EnableRecorders()
        {
            if (_recordersActive) return;

            _drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _setPassRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _trianglesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _verticesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            _shadowCastersRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
            _visibleSkinnedMeshesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Visible Skinned Meshes Count");

            _gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _totalMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            _gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
            _gfxMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");
            _textureMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory");
            _meshMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Mesh Memory");
            _renderTexturesCountRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Render Textures Count");
            _renderTexturesBytesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Render Textures Bytes");
            _usedTexturesCountRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Used Textures Count");
            _usedTexturesBytesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Used Textures Bytes");

            _mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            _renderThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 15);

            _recordersActive = true;
        }

        private void DisposeRecorders()
        {
            if (!_recordersActive) return;

            _drawCallsRecorder.Dispose();
            _batchesRecorder.Dispose();
            _setPassRecorder.Dispose();
            _trianglesRecorder.Dispose();
            _verticesRecorder.Dispose();
            _shadowCastersRecorder.Dispose();
            _visibleSkinnedMeshesRecorder.Dispose();
            _gcAllocRecorder.Dispose();
            _totalMemoryRecorder.Dispose();
            _gcMemoryRecorder.Dispose();
            _gfxMemoryRecorder.Dispose();
            _textureMemoryRecorder.Dispose();
            _meshMemoryRecorder.Dispose();
            _renderTexturesCountRecorder.Dispose();
            _renderTexturesBytesRecorder.Dispose();
            _usedTexturesCountRecorder.Dispose();
            _usedTexturesBytesRecorder.Dispose();
            _mainThreadRecorder.Dispose();
            _renderThreadRecorder.Dispose();

            _recordersActive = false;
        }

        private void SaveAndUncapSettings()
        {
            _savedVSyncCount = QualitySettings.vSyncCount;
            _savedTargetFrameRate = Application.targetFrameRate;
            _settingsSaved = true;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        private void RestoreSettings()
        {
            if (!_settingsSaved) return;
            QualitySettings.vSyncCount = _savedVSyncCount;
            Application.targetFrameRate = _savedTargetFrameRate;
            _settingsSaved = false;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
            {
                if (_isBenchmarking) StopBenchmark();
                else StartBenchmark();
            }
            if (Input.GetKeyDown(KeyCode.F9))
            {
                benchmarkDuration = 5f;
                StartBenchmark();
            }

            if (!_isBenchmarking) return;

            if (_isWarmingUp)
            {
                _warmupRemaining--;
                _currentFPS = 1f / Time.unscaledDeltaTime;
                if (_warmupRemaining <= 0)
                {
                    _isWarmingUp = false;
                    _benchmarkStartTime = Time.realtimeSinceStartup;
                    _frameCount = 0;
                    Debug.Log("[Benchmark] Warmup complete. Recording...");
                }
                return;
            }

            // GPU timing: ONLY call if user opted in
            // FrameTimingManager.CaptureFrameTimings() forces a GPU sync point every frame
            // = CPU waits for GPU to finish = destroys CPU/GPU parallelism = ~50-100 FPS loss
            if (enableGpuTiming)
                FrameTimingManager.CaptureFrameTimings();

            RecordFrame();

            if (cameraPath != null && cameraPath.Length >= 2)
                UpdateCameraPath();

            float elapsed = Time.realtimeSinceStartup - _benchmarkStartTime;
            _progress = Mathf.Clamp01(elapsed / benchmarkDuration);
            _currentFPS = 1f / Time.unscaledDeltaTime;
            _recordedFrames = _frameCount;

            if (elapsed >= benchmarkDuration)
                StopBenchmark();
        }

        private void RecordFrame()
        {
            if (_frameCount >= _frameBuffer.Length)
            {
                var newBuf = new FrameData[_frameBuffer.Length * 2];
                Array.Copy(_frameBuffer, newBuf, _frameCount);
                _frameBuffer = newBuf;
            }

            ref var data = ref _frameBuffer[_frameCount];

            data.deltaTime = Time.unscaledDeltaTime;
            data.cpuMainMs = GetRecorderMs(_mainThreadRecorder);
            data.cpuRenderMs = GetRecorderMs(_renderThreadRecorder);

            if (enableGpuTiming)
            {
                uint timingCount = FrameTimingManager.GetLatestTimings(1, _frameTimings);
                if (timingCount > 0)
                {
                    data.gpuMs = (float)_frameTimings[0].gpuFrameTime;
                    if (_frameTimings[0].cpuFrameTime > 0)
                        data.cpuMainMs = (float)_frameTimings[0].cpuFrameTime;
                }
                else
                {
                    data.gpuMs = 0f;
                }
            }
            else
            {
                data.gpuMs = 0f;
            }

            data.drawCalls = GetRecorderValue(_drawCallsRecorder);
            data.batches = GetRecorderValue(_batchesRecorder);
            data.setPassCalls = GetRecorderValue(_setPassRecorder);
            data.triangles = GetRecorderLong(_trianglesRecorder);
            data.vertices = GetRecorderLong(_verticesRecorder);
            data.shadowCasters = GetRecorderValue(_shadowCastersRecorder);
            data.visibleSkinnedMeshes = GetRecorderValue(_visibleSkinnedMeshesRecorder);
            data.gcAllocBytes = GetRecorderLong(_gcAllocRecorder);

            _frameCount++;
        }

        private static int GetRecorderValue(ProfilerRecorder recorder)
        {
            return recorder.Valid && recorder.Count > 0 ? (int)recorder.LastValue : 0;
        }

        private static long GetRecorderLong(ProfilerRecorder recorder)
        {
            return recorder.Valid && recorder.Count > 0 ? recorder.LastValue : 0;
        }

        private static float GetRecorderMs(ProfilerRecorder recorder)
        {
            return recorder.Valid && recorder.Count > 0
                ? recorder.LastValue * 1e-6f
                : 0f;
        }

        public void StartBenchmark()
        {
            if (_isBenchmarking) return;

            SaveAndUncapSettings();
            EnableRecorders();

            int estimate = Mathf.Max(8192, (int)(500f * benchmarkDuration * 2f));
            if (_frameBuffer == null || _frameBuffer.Length < estimate)
                _frameBuffer = new FrameData[estimate];
            _frameCount = 0;

            _warmupRemaining = warmupFrames;
            _isWarmingUp = warmupFrames > 0;
            _isBenchmarking = true;
            _progress = 0;
            _recordedFrames = 0;
            _cameraPathIndex = 0;
            _cameraPathT = 0;

            if (!_isWarmingUp)
                _benchmarkStartTime = Time.realtimeSinceStartup;

            Debug.Log($"[Benchmark] Started — duration={benchmarkDuration}s, warmup={warmupFrames}, " +
                $"label='{configLabel}', gpuTiming={enableGpuTiming}, overlay={showOverlay}");
        }

        public void StopBenchmark()
        {
            if (!_isBenchmarking) return;
            _isBenchmarking = false;
            _isWarmingUp = false;

            RestoreSettings();

            if (_frameCount < 2)
            {
                DisposeRecorders();
                Debug.LogWarning("[Benchmark] Not enough frames recorded. Try longer duration.");
                return;
            }

            LastResult = BuildResult();
            DisposeRecorders();

            string path = ExportReport(LastResult);

            Debug.Log($"[Benchmark] Complete — {LastResult.totalFrames} frames, " +
                $"Avg FPS: {LastResult.avgFPS:F1}, " +
                $"1% Low: {LastResult.fps1Low:F1}, " +
                $"Batches: {LastResult.avgBatches:F0}, " +
                $"Report: {path}");

            OnBenchmarkComplete?.Invoke(this);
        }

        public void SetAsBefore()
        {
            _beforeResult = LastResult;
            Debug.Log("[Benchmark] Saved as BEFORE snapshot.");
        }

        public void GenerateComparison()
        {
            if (_beforeResult == null || LastResult == null)
            {
                Debug.LogWarning("[Benchmark] Need both BEFORE and AFTER results.");
                return;
            }
            ExportComparisonReport(_beforeResult, LastResult);
        }

        [Serializable]
        public class BenchmarkResult
        {
            public string label;
            public string timestamp;
            public string sceneName;
            public float durationSeconds;
            public int totalFrames;
            public string resolution;

            public float avgFPS, medianFPS, minFPS, maxFPS;
            public float fps1Low, fps01Low;
            public float fpsStdDev;

            public float avgFrameTime, maxFrameTime, minFrameTime;
            public float frameTime95th, frameTime99th, frameTime999th;
            public float frameTimeStdDev;

            public float avgCpuMain, maxCpuMain, minCpuMain;
            public float avgCpuRender, maxCpuRender;
            public float cpuMainStdDev;

            public float avgGpu, maxGpu, minGpu;
            public float gpuStdDev;
            public bool gpuDataAvailable;

            public float avgDrawCalls, maxDrawCalls, minDrawCalls;
            public float avgBatches, maxBatches, minBatches;
            public float avgSetPass, maxSetPass;
            public float avgTriangles, maxTriangles;
            public float avgVertices, maxVertices;
            public float avgShadowCasters, maxShadowCasters;
            public float avgVisibleSkinned;

            public long totalMemoryMB;
            public long gcMemoryMB;
            public long gfxMemoryMB;
            public long textureMemoryMB;
            public long meshMemoryMB;
            public int renderTextureCount;
            public long renderTexturesMB;
            public int usedTextureCount;
            public long usedTexturesMB;

            public float avgGcAllocPerFrame;
            public long maxGcAllocPerFrame;
            public long totalGcAlloc;
            public int gcSpikeFrames;

            public int stutterFrames;
            public float stutterPercent;
            public int severeStutterFrames;
            public float longestStutterMs;

            public string bottleneck;

            public int[] frameTimeBuckets;
        }

        private BenchmarkResult BuildResult()
        {
            var r = new BenchmarkResult();
            int n = _frameCount;

            r.label = configLabel;
            r.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            r.sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            r.durationSeconds = Time.realtimeSinceStartup - _benchmarkStartTime;
            r.totalFrames = n;
            r.resolution = $"{Screen.width}x{Screen.height}";

            var fpsList = new float[n];
            var frameTimes = new float[n];
            for (int i = 0; i < n; i++)
            {
                frameTimes[i] = _frameBuffer[i].deltaTime * 1000f;
                fpsList[i] = 1f / Mathf.Max(_frameBuffer[i].deltaTime, 0.00001f);
            }

            Array.Sort(fpsList);
            Array.Sort(frameTimes);

            float fpsSum = 0;
            for (int i = 0; i < n; i++) fpsSum += fpsList[i];
            r.avgFPS = fpsSum / n;
            r.medianFPS = fpsList[n / 2];
            r.minFPS = fpsList[0];
            r.maxFPS = fpsList[n - 1];
            r.fps1Low = AverageBottom(fpsList, 0.01f);
            r.fps01Low = AverageBottom(fpsList, 0.001f);
            r.fpsStdDev = StdDev(fpsList, r.avgFPS);

            float ftSum = 0;
            for (int i = 0; i < n; i++) ftSum += frameTimes[i];
            r.avgFrameTime = ftSum / n;
            r.minFrameTime = frameTimes[0];
            r.maxFrameTime = frameTimes[n - 1];
            r.frameTime95th = Percentile(frameTimes, 0.95f);
            r.frameTime99th = Percentile(frameTimes, 0.99f);
            r.frameTime999th = Percentile(frameTimes, 0.999f);
            r.frameTimeStdDev = StdDev(frameTimes, r.avgFrameTime);

            float cpuMainSum = 0, cpuRenderSum = 0;
            float cpuMainMax = float.MinValue, cpuMainMin = float.MaxValue;
            float cpuRenderMax = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                float cm = _frameBuffer[i].cpuMainMs;
                float cr = _frameBuffer[i].cpuRenderMs;
                cpuMainSum += cm;
                cpuRenderSum += cr;
                if (cm > cpuMainMax) cpuMainMax = cm;
                if (cm < cpuMainMin) cpuMainMin = cm;
                if (cr > cpuRenderMax) cpuRenderMax = cr;
            }
            r.avgCpuMain = cpuMainSum / n;
            r.maxCpuMain = cpuMainMax;
            r.minCpuMain = cpuMainMin;
            r.avgCpuRender = cpuRenderSum / n;
            r.maxCpuRender = cpuRenderMax;

            float cpuSqSum = 0;
            for (int i = 0; i < n; i++)
            {
                float d = _frameBuffer[i].cpuMainMs - r.avgCpuMain;
                cpuSqSum += d * d;
            }
            r.cpuMainStdDev = n > 1 ? Mathf.Sqrt(cpuSqSum / (n - 1)) : 0f;

            int gpuValidCount = 0;
            float gpuSum = 0;
            float gpuMax = float.MinValue, gpuMin = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float g = _frameBuffer[i].gpuMs;
                if (g > 0)
                {
                    gpuValidCount++;
                    gpuSum += g;
                    if (g > gpuMax) gpuMax = g;
                    if (g < gpuMin) gpuMin = g;
                }
            }
            r.gpuDataAvailable = gpuValidCount > n * 0.5f;
            if (gpuValidCount > 0)
            {
                r.avgGpu = gpuSum / gpuValidCount;
                r.maxGpu = gpuMax;
                r.minGpu = gpuMin;

                float gpuSqSum = 0;
                for (int i = 0; i < n; i++)
                {
                    float g = _frameBuffer[i].gpuMs;
                    if (g > 0)
                    {
                        float d = g - r.avgGpu;
                        gpuSqSum += d * d;
                    }
                }
                r.gpuStdDev = gpuValidCount > 1 ? Mathf.Sqrt(gpuSqSum / (gpuValidCount - 1)) : 0f;
            }

            float dcSum = 0, dcMax = float.MinValue, dcMin = float.MaxValue;
            float bSum = 0, bMax = float.MinValue, bMin = float.MaxValue;
            float spSum = 0, spMax = float.MinValue;
            float triSum = 0, triMax = float.MinValue;
            float vertSum = 0, vertMax = float.MinValue;
            float scSum = 0, scMax = float.MinValue;
            float skSum = 0;
            for (int i = 0; i < n; i++)
            {
                ref var f = ref _frameBuffer[i];
                float dc = f.drawCalls; float b = f.batches; float sp = f.setPassCalls;
                float tri = f.triangles; float vert = f.vertices;
                float sc = f.shadowCasters; float sk = f.visibleSkinnedMeshes;

                dcSum += dc; if (dc > dcMax) dcMax = dc; if (dc < dcMin) dcMin = dc;
                bSum += b; if (b > bMax) bMax = b; if (b < bMin) bMin = b;
                spSum += sp; if (sp > spMax) spMax = sp;
                triSum += tri; if (tri > triMax) triMax = tri;
                vertSum += vert; if (vert > vertMax) vertMax = vert;
                scSum += sc; if (sc > scMax) scMax = sc;
                skSum += sk;
            }
            r.avgDrawCalls = dcSum / n; r.maxDrawCalls = dcMax; r.minDrawCalls = dcMin;
            r.avgBatches = bSum / n; r.maxBatches = bMax; r.minBatches = bMin;
            r.avgSetPass = spSum / n; r.maxSetPass = spMax;
            r.avgTriangles = triSum / n; r.maxTriangles = triMax;
            r.avgVertices = vertSum / n; r.maxVertices = vertMax;
            r.avgShadowCasters = scSum / n; r.maxShadowCasters = scMax;
            r.avgVisibleSkinned = skSum / n;

            r.totalMemoryMB = GetRecorderLong(_totalMemoryRecorder) / (1024 * 1024);
            r.gcMemoryMB = GetRecorderLong(_gcMemoryRecorder) / (1024 * 1024);
            r.gfxMemoryMB = GetRecorderLong(_gfxMemoryRecorder) / (1024 * 1024);
            r.textureMemoryMB = GetRecorderLong(_textureMemoryRecorder) / (1024 * 1024);
            r.meshMemoryMB = GetRecorderLong(_meshMemoryRecorder) / (1024 * 1024);
            r.renderTextureCount = GetRecorderValue(_renderTexturesCountRecorder);
            r.renderTexturesMB = GetRecorderLong(_renderTexturesBytesRecorder) / (1024 * 1024);
            r.usedTextureCount = GetRecorderValue(_usedTexturesCountRecorder);
            r.usedTexturesMB = GetRecorderLong(_usedTexturesBytesRecorder) / (1024 * 1024);

            if (r.totalMemoryMB == 0)
            {
                r.totalMemoryMB = Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
                r.gcMemoryMB = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);
                r.gfxMemoryMB = Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024 * 1024);
            }

            long totalGc = 0, maxGc = 0;
            int gcSpikes = 0;
            for (int i = 0; i < n; i++)
            {
                long gc = _frameBuffer[i].gcAllocBytes;
                totalGc += gc;
                if (gc > maxGc) maxGc = gc;
                if (gc > 1024) gcSpikes++;
            }
            r.totalGcAlloc = totalGc;
            r.avgGcAllocPerFrame = (float)totalGc / n;
            r.maxGcAllocPerFrame = maxGc;
            r.gcSpikeFrames = gcSpikes;

            float avgDt = r.avgFrameTime;
            int stutters = 0, severe = 0;
            float longestStutter = 0;
            for (int i = 0; i < n; i++)
            {
                float ms = _frameBuffer[i].deltaTime * 1000f;
                if (ms > avgDt * 2f) stutters++;
                if (ms > avgDt * 3f) severe++;
                if (ms > longestStutter) longestStutter = ms;
            }
            r.stutterFrames = stutters;
            r.stutterPercent = (float)stutters / n * 100f;
            r.severeStutterFrames = severe;
            r.longestStutterMs = longestStutter;

            if (r.gpuDataAvailable)
            {
                if (r.avgCpuMain > r.avgGpu * 1.3f) r.bottleneck = "CPU BOUND";
                else if (r.avgGpu > r.avgCpuMain * 1.3f) r.bottleneck = "GPU BOUND";
                else r.bottleneck = "BALANCED";
            }
            else
            {
                r.bottleneck = "CPU BOUND (no GPU data)";
            }

            r.frameTimeBuckets = new int[8];
            for (int i = 0; i < n; i++)
            {
                float ms = _frameBuffer[i].deltaTime * 1000f;
                if (ms < 8) r.frameTimeBuckets[0]++;
                else if (ms < 11.1f) r.frameTimeBuckets[1]++;
                else if (ms < 16.7f) r.frameTimeBuckets[2]++;
                else if (ms < 22.2f) r.frameTimeBuckets[3]++;
                else if (ms < 33.3f) r.frameTimeBuckets[4]++;
                else if (ms < 50f) r.frameTimeBuckets[5]++;
                else if (ms < 100f) r.frameTimeBuckets[6]++;
                else r.frameTimeBuckets[7]++;
            }

            return r;
        }

        // ══════════════════════════════════════════════════════
        //  REPORT EXPORT — EXACT SAME FORMAT AS ORIGINAL v2
        // ══════════════════════════════════════════════════════

        private string ExportReport(BenchmarkResult r)
        {
            var sb = new StringBuilder(4096);
            string sep = new string('═', 75);
            string thin = new string('─', 75);

            sb.AppendLine(sep);
            sb.AppendLine("  PERFORMANCE BENCHMARK REPORT v2");
            sb.AppendLine($"  Label:      {r.label}");
            sb.AppendLine($"  Scene:      {r.sceneName}");
            sb.AppendLine($"  Time:       {r.timestamp}");
            sb.AppendLine($"  Duration:   {r.durationSeconds:F1}s ({r.totalFrames} frames)");
            sb.AppendLine($"  Resolution: {r.resolution}");
            sb.AppendLine($"  Bottleneck: {r.bottleneck}");
            sb.AppendLine(sep);
            sb.AppendLine();

            sb.AppendLine($"── FPS {thin.Substring(6)}");
            sb.AppendLine($"  Average:           {r.avgFPS,10:F1}");
            sb.AppendLine($"  Median:            {r.medianFPS,10:F1}");
            sb.AppendLine($"  Min:               {r.minFPS,10:F1}");
            sb.AppendLine($"  Max:               {r.maxFPS,10:F1}");
            sb.AppendLine($"  1% Low:            {r.fps1Low,10:F1}");
            sb.AppendLine($"  0.1% Low:          {r.fps01Low,10:F1}");
            sb.AppendLine($"  Std Dev:           {r.fpsStdDev,10:F1}");
            sb.AppendLine($"  Stability:         {(r.fpsStdDev < r.avgFPS * 0.05f ? "STABLE" : r.fpsStdDev < r.avgFPS * 0.15f ? "MODERATE" : "UNSTABLE"),10}");
            sb.AppendLine();

            sb.AppendLine($"── FRAME TIME (ms) {thin.Substring(18)}");
            sb.AppendLine($"  Average:           {r.avgFrameTime,10:F2}");
            sb.AppendLine($"  Min:               {r.minFrameTime,10:F2}");
            sb.AppendLine($"  Max:               {r.maxFrameTime,10:F2}");
            sb.AppendLine($"  95th Percentile:   {r.frameTime95th,10:F2}");
            sb.AppendLine($"  99th Percentile:   {r.frameTime99th,10:F2}");
            sb.AppendLine($"  99.9th Percentile: {r.frameTime999th,10:F2}");
            sb.AppendLine($"  Std Dev:           {r.frameTimeStdDev,10:F2}");
            sb.AppendLine();

            sb.AppendLine($"── CPU TIMING (ms) {thin.Substring(18)}");
            sb.AppendLine($"  Main Thread Avg:   {r.avgCpuMain,10:F2}");
            sb.AppendLine($"  Main Thread Max:   {r.maxCpuMain,10:F2}");
            sb.AppendLine($"  Main Thread Min:   {r.minCpuMain,10:F2}");
            sb.AppendLine($"  Main Thread σ:     {r.cpuMainStdDev,10:F2}");
            sb.AppendLine($"  Render Thread Avg: {r.avgCpuRender,10:F2}");
            sb.AppendLine($"  Render Thread Max: {r.maxCpuRender,10:F2}");
            sb.AppendLine();

            sb.AppendLine($"── GPU TIMING (ms) {thin.Substring(18)}");
            if (r.gpuDataAvailable)
            {
                sb.AppendLine($"  Average:           {r.avgGpu,10:F2}");
                sb.AppendLine($"  Max:               {r.maxGpu,10:F2}");
                sb.AppendLine($"  Min:               {r.minGpu,10:F2}");
                sb.AppendLine($"  Std Dev:           {r.gpuStdDev,10:F2}");
            }
            else
            {
                sb.AppendLine("  (GPU timing not available — enable FrameTimingManager in Player Settings)");
            }
            sb.AppendLine();

            sb.AppendLine($"── RENDERING STATS {thin.Substring(18)}");
            sb.AppendLine($"  {"",25} {"Avg",10} {"Min",10} {"Max",10}");
            sb.AppendLine($"  {"Draw Calls",-25} {r.avgDrawCalls,10:F0} {r.minDrawCalls,10:F0} {r.maxDrawCalls,10:F0}");
            sb.AppendLine($"  {"Batches",-25} {r.avgBatches,10:F0} {r.minBatches,10:F0} {r.maxBatches,10:F0}");
            sb.AppendLine($"  {"SetPass Calls",-25} {r.avgSetPass,10:F0} {"",10} {r.maxSetPass,10:F0}");
            sb.AppendLine($"  {"Triangles",-25} {FormatK(r.avgTriangles),10} {"",10} {FormatK(r.maxTriangles),10}");
            sb.AppendLine($"  {"Vertices",-25} {FormatK(r.avgVertices),10} {"",10} {FormatK(r.maxVertices),10}");
            sb.AppendLine($"  {"Shadow Casters",-25} {r.avgShadowCasters,10:F0} {"",10} {r.maxShadowCasters,10:F0}");
            sb.AppendLine($"  {"Visible Skinned Meshes",-25} {r.avgVisibleSkinned,10:F0}");
            sb.AppendLine();

            sb.AppendLine($"── MEMORY {thin.Substring(9)}");
            sb.AppendLine($"  Total Used:        {r.totalMemoryMB,10} MB");
            sb.AppendLine($"  GC Heap:           {r.gcMemoryMB,10} MB");
            sb.AppendLine($"  Graphics:          {r.gfxMemoryMB,10} MB");
            sb.AppendLine($"  Textures:          {r.textureMemoryMB,10} MB  ({r.usedTextureCount} textures)");
            sb.AppendLine($"  Meshes:            {r.meshMemoryMB,10} MB");
            sb.AppendLine($"  Render Textures:   {r.renderTexturesMB,10} MB  ({r.renderTextureCount} RTs)");
            sb.AppendLine();

            sb.AppendLine($"── GC ALLOCATION {thin.Substring(16)}");
            sb.AppendLine($"  Avg Per Frame:     {FormatBytes(r.avgGcAllocPerFrame),10}");
            sb.AppendLine($"  Max Per Frame:     {FormatBytes(r.maxGcAllocPerFrame),10}");
            sb.AppendLine($"  Total During Test: {FormatBytes(r.totalGcAlloc),10}");
            sb.AppendLine($"  Spike Frames:      {r.gcSpikeFrames,10} ({(float)r.gcSpikeFrames / r.totalFrames * 100f:F1}% of frames > 1KB)");
            sb.AppendLine($"  Health:            {(r.avgGcAllocPerFrame < 1024 ? "CLEAN" : r.avgGcAllocPerFrame < 4096 ? "ACCEPTABLE" : r.avgGcAllocPerFrame < 32768 ? "WARNING" : "CRITICAL"),10}");
            sb.AppendLine();

            sb.AppendLine($"── FRAME STABILITY {thin.Substring(18)}");
            sb.AppendLine($"  Stutter (>2x avg): {r.stutterFrames,10} frames ({r.stutterPercent:F1}%)");
            sb.AppendLine($"  Severe  (>3x avg): {r.severeStutterFrames,10} frames");
            sb.AppendLine($"  Longest Spike:     {r.longestStutterMs,10:F1} ms");
            sb.AppendLine($"  Rating:            {(r.stutterPercent < 0.5f ? "SMOOTH" : r.stutterPercent < 2f ? "ACCEPTABLE" : r.stutterPercent < 5f ? "NOTICEABLE" : "POOR"),10}");
            sb.AppendLine();

            string[] bucketLabels = {
                "<8ms (>120fps)", "8-11ms (90-120)", "11-17ms (60-90)",
                "17-22ms (45-60)", "22-33ms (30-45)", "33-50ms (20-30)",
                "50-100ms (10-20)", ">100ms (<10fps)"
            };
            sb.AppendLine($"── FRAME TIME DISTRIBUTION {thin.Substring(25)}");
            for (int i = 0; i < 8; i++)
            {
                float pct = (float)r.frameTimeBuckets[i] / r.totalFrames * 100f;
                int barLen = Mathf.Min(40, (int)(pct / 2.5f));
                string bar = new string('█', barLen) + new string('░', 40 - barLen);
                sb.AppendLine($"  {bucketLabels[i],-22} {bar} {pct,5:F1}% ({r.frameTimeBuckets[i]})");
            }
            sb.AppendLine();

            sb.AppendLine($"── VR READINESS (Quest Target) {thin.Substring(30)}");
            bool batchOk = r.avgBatches <= 100;
            bool triOk = r.avgTriangles <= 200000;
            bool fpsOk = r.fps1Low >= 72;
            bool gpuOk = !r.gpuDataAvailable || r.avgGpu <= 11;
            bool gcOk = r.avgGcAllocPerFrame < 4096;
            sb.AppendLine($"  Batches ≤100:      {(batchOk ? "✓ PASS" : "✗ FAIL"),10}  (avg: {r.avgBatches:F0})");
            sb.AppendLine($"  Triangles ≤200K:   {(triOk ? "✓ PASS" : "✗ FAIL"),10}  (avg: {FormatK(r.avgTriangles)})");
            sb.AppendLine($"  1% Low FPS ≥72:    {(fpsOk ? "✓ PASS" : "✗ FAIL"),10}  (1% low: {r.fps1Low:F1})");
            sb.AppendLine($"  GPU ≤11ms:         {(gpuOk ? "✓ PASS" : "? N/A"),10}  (avg: {(r.gpuDataAvailable ? $"{r.avgGpu:F1}ms" : "no data")})");
            sb.AppendLine($"  GC ≤4KB/frame:     {(gcOk ? "✓ PASS" : "✗ FAIL"),10}  (avg: {FormatBytes(r.avgGcAllocPerFrame)})");
            int passed = (batchOk ? 1 : 0) + (triOk ? 1 : 0) + (fpsOk ? 1 : 0) + (gpuOk ? 1 : 0) + (gcOk ? 1 : 0);
            sb.AppendLine($"  Overall:           {passed}/5 checks passed {(passed >= 4 ? "— READY" : passed >= 3 ? "— ALMOST" : "— NOT READY")}");
            sb.AppendLine();

            string filename = $"Benchmark_{r.label}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string path = GetOutputPath(filename);
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        private void ExportComparisonReport(BenchmarkResult before, BenchmarkResult after)
        {
            var sb = new StringBuilder(4096);
            string sep = new string('═', 75);
            string thin = new string('─', 75);

            sb.AppendLine(sep);
            sb.AppendLine("  PERFORMANCE COMPARISON REPORT");
            sb.AppendLine($"  Before: {before.label} ({before.timestamp})");
            sb.AppendLine($"  After:  {after.label} ({after.timestamp})");
            sb.AppendLine($"  Scene:  {before.sceneName}");
            sb.AppendLine(sep);
            sb.AppendLine();

            float fpsChange = Pct(before.avgFPS, after.avgFPS, true);
            float cpuChange = Pct(before.avgCpuMain, after.avgCpuMain, false);
            float gpuChange = Pct(before.avgGpu, after.avgGpu, false);
            float batchChange = Pct(before.avgBatches, after.avgBatches, false);
            float triChange = Pct(before.avgTriangles, after.avgTriangles, false);
            float gcChange = Pct(before.avgGcAllocPerFrame, after.avgGcAllocPerFrame, false);

            sb.AppendLine("┌───────────────────────────────────────────────────────┐");
            sb.AppendLine("│               IMPROVEMENT SUMMARY                     │");
            sb.AppendLine("├───────────────────────────────────────────────────────┤");
            sb.AppendLine($"│  FPS:          {FmtPct(fpsChange, true),40} │");
            sb.AppendLine($"│  CPU Time:     {FmtPct(cpuChange, false),40} │");
            sb.AppendLine($"│  GPU Time:     {FmtPct(gpuChange, false),40} │");
            sb.AppendLine($"│  Batches:      {FmtPct(batchChange, false),40} │");
            sb.AppendLine($"│  Triangles:    {FmtPct(triChange, false),40} │");
            sb.AppendLine($"│  GC Alloc:     {FmtPct(gcChange, false),40} │");
            sb.AppendLine("└───────────────────────────────────────────────────────┘");
            sb.AppendLine();

            sb.AppendLine($"  {"Metric",-28} {"BEFORE",12} {"AFTER",12} {"CHANGE",12}");
            sb.AppendLine($"  {thin.Substring(2)}");
            Row(sb, "Avg FPS", F1(before.avgFPS), F1(after.avgFPS), Delta(before.avgFPS, after.avgFPS, true));
            Row(sb, "1% Low FPS", F1(before.fps1Low), F1(after.fps1Low), Delta(before.fps1Low, after.fps1Low, true));
            Row(sb, "Min FPS", F1(before.minFPS), F1(after.minFPS), Delta(before.minFPS, after.minFPS, true));
            Row(sb, "FPS Std Dev", F1(before.fpsStdDev), F1(after.fpsStdDev), Delta(before.fpsStdDev, after.fpsStdDev, false));
            sb.AppendLine();
            Row(sb, "Avg Frame Time (ms)", F2(before.avgFrameTime), F2(after.avgFrameTime), Delta(before.avgFrameTime, after.avgFrameTime, false));
            Row(sb, "99th Pctile (ms)", F2(before.frameTime99th), F2(after.frameTime99th), Delta(before.frameTime99th, after.frameTime99th, false));
            sb.AppendLine();
            Row(sb, "CPU Main (ms)", F2(before.avgCpuMain), F2(after.avgCpuMain), Delta(before.avgCpuMain, after.avgCpuMain, false));
            Row(sb, "CPU Render (ms)", F2(before.avgCpuRender), F2(after.avgCpuRender), Delta(before.avgCpuRender, after.avgCpuRender, false));
            Row(sb, "GPU (ms)", F2(before.avgGpu), F2(after.avgGpu), Delta(before.avgGpu, after.avgGpu, false));
            sb.AppendLine();
            Row(sb, "Avg Batches", F0(before.avgBatches), F0(after.avgBatches), Delta(before.avgBatches, after.avgBatches, false));
            Row(sb, "Avg Draw Calls", F0(before.avgDrawCalls), F0(after.avgDrawCalls), Delta(before.avgDrawCalls, after.avgDrawCalls, false));
            Row(sb, "Avg SetPass", F0(before.avgSetPass), F0(after.avgSetPass), Delta(before.avgSetPass, after.avgSetPass, false));
            Row(sb, "Avg Triangles", FormatK(before.avgTriangles), FormatK(after.avgTriangles), Delta(before.avgTriangles, after.avgTriangles, false));
            Row(sb, "Avg Vertices", FormatK(before.avgVertices), FormatK(after.avgVertices), Delta(before.avgVertices, after.avgVertices, false));
            sb.AppendLine();
            Row(sb, "GC Alloc/Frame", FormatBytes(before.avgGcAllocPerFrame), FormatBytes(after.avgGcAllocPerFrame), Delta(before.avgGcAllocPerFrame, after.avgGcAllocPerFrame, false));
            Row(sb, "GC Spikes", $"{before.gcSpikeFrames}", $"{after.gcSpikeFrames}", Delta(before.gcSpikeFrames, after.gcSpikeFrames, false));
            Row(sb, "Stutter %", $"{before.stutterPercent:F1}%", $"{after.stutterPercent:F1}%", Delta(before.stutterPercent, after.stutterPercent, false));
            Row(sb, "Total Memory (MB)", $"{before.totalMemoryMB}", $"{after.totalMemoryMB}", Delta(before.totalMemoryMB, after.totalMemoryMB, false));
            Row(sb, "GFX Memory (MB)", $"{before.gfxMemoryMB}", $"{after.gfxMemoryMB}", Delta(before.gfxMemoryMB, after.gfxMemoryMB, false));
            sb.AppendLine();

            sb.AppendLine(sep);
            sb.AppendLine("  VERDICT");
            sb.AppendLine(sep);
            if (fpsChange > 5) sb.AppendLine($"  ✓ FPS improved {fpsChange:F1}%");
            else if (fpsChange < -5) sb.AppendLine($"  ✗ FPS regressed {-fpsChange:F1}%");
            else sb.AppendLine($"  ─ FPS unchanged ({fpsChange:+0.0;-0.0}%)");

            if (batchChange < -10) sb.AppendLine($"  ✓ Batches reduced {-batchChange:F0}%");
            if (cpuChange < -10) sb.AppendLine($"  ✓ CPU time reduced {-cpuChange:F1}%");
            if (gpuChange < -10) sb.AppendLine($"  ✓ GPU time reduced {-gpuChange:F1}%");
            if (gcChange < -50) sb.AppendLine($"  ✓ GC alloc reduced {-gcChange:F0}%");

            sb.AppendLine();

            string filename = $"Comparison_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string path = GetOutputPath(filename);
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[Benchmark] Comparison saved: {path}");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(path);
#endif
        }

        private void UpdateCameraPath()
        {
            if (Camera.main == null) return;
            _cameraPathT += Time.deltaTime * pathSpeed /
                Mathf.Max(0.01f, Vector3.Distance(
                    cameraPath[_cameraPathIndex].position,
                    cameraPath[(_cameraPathIndex + 1) % cameraPath.Length].position));

            if (_cameraPathT >= 1f)
            {
                _cameraPathT = 0f;
                _cameraPathIndex = (_cameraPathIndex + 1) % cameraPath.Length;
            }

            int next = (_cameraPathIndex + 1) % cameraPath.Length;
            Camera.main.transform.position = Vector3.Lerp(
                cameraPath[_cameraPathIndex].position,
                cameraPath[next].position, _cameraPathT);
            Camera.main.transform.rotation = Quaternion.Slerp(
                cameraPath[_cameraPathIndex].rotation,
                cameraPath[next].rotation, _cameraPathT);
        }

        private static float AverageBottom(float[] sorted, float fraction)
        {
            int count = Mathf.Max(1, (int)(sorted.Length * fraction));
            float sum = 0;
            for (int i = 0; i < count; i++) sum += sorted[i];
            return sum / count;
        }

        private static float Percentile(float[] sorted, float p)
        {
            int idx = Mathf.Min(sorted.Length - 1, (int)(sorted.Length * p));
            return sorted[idx];
        }

        private static float StdDev(float[] values, float mean)
        {
            if (values.Length < 2) return 0;
            float sumSq = 0;
            for (int i = 0; i < values.Length; i++)
            {
                float d = values[i] - mean;
                sumSq += d * d;
            }
            return Mathf.Sqrt(sumSq / (values.Length - 1));
        }

        private static float Pct(float before, float after, bool higherBetter)
        {
            if (Mathf.Abs(before) < 0.001f) return 0;
            return (after - before) / before * 100f;
        }

        private static string FormatBytes(float bytes)
        {
            if (bytes < 1024) return $"{bytes:F0} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
            return $"{bytes / (1024f * 1024f):F1} MB";
        }

        private static string FormatBytes(long bytes) => FormatBytes((float)bytes);

        private static string FormatK(float value)
        {
            if (value < 1000) return $"{value:F0}";
            if (value < 1000000) return $"{value / 1000f:F1}K";
            return $"{value / 1000000f:F2}M";
        }

        private static string F0(float v) => $"{v:F0}";
        private static string F1(float v) => $"{v:F1}";
        private static string F2(float v) => $"{v:F2}";

        private static string FmtPct(float pct, bool higherBetter)
        {
            string arrow = pct > 1 ? (higherBetter ? "▲" : "▼") :
                           pct < -1 ? (higherBetter ? "▼" : "▲") : "─";
            return $"{arrow} {(pct > 0 ? "+" : "")}{pct:F1}%";
        }

        private static string Delta(float before, float after, bool higherBetter)
        {
            float diff = after - before;
            if (Mathf.Abs(diff) < 0.01f) return "  =";
            string icon = (diff > 0 && higherBetter) || (diff < 0 && !higherBetter) ? "✓" : "✗";
            return $"{icon} {(diff > 0 ? "+" : "")}{diff:F1}";
        }

        private static string Delta(long before, long after, bool higherBetter)
        {
            return Delta((float)before, (float)after, higherBetter);
        }

        private static void Row(StringBuilder sb, string metric, string before, string after, string change)
        {
            sb.AppendLine($"  {metric,-28} {before,12} {after,12} {change,12}");
        }

        private string GetOutputPath(string filename)
        {
            string dir = Path.Combine(Application.dataPath, "..", outputFolder);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, filename);
        }

        // ── OnGUI — ONLY when overlay enabled AND benchmarking ──

        private GUIStyle _guiStyle;
        private GUIStyle _guiBoldStyle;
        private Texture2D _bgTexture;

        private void OnGUI()
        {
            if (!showOverlay || !_isBenchmarking) return;

            if (_guiStyle == null)
            {
                _bgTexture = new Texture2D(1, 1);
                _bgTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.8f));
                _bgTexture.Apply();

                _guiStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    normal = { textColor = Color.white }
                };
                _guiBoldStyle = new GUIStyle(_guiStyle)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 14
                };
            }

            float x = 10, y = 10, w = 280;

            GUI.DrawTexture(new Rect(x - 5, y - 5, w + 10, _isWarmingUp ? 50 : 110), _bgTexture);

            if (_isWarmingUp)
            {
                GUI.Label(new Rect(x, y, w, 20), $"WARMING UP... ({_warmupRemaining})", _guiBoldStyle);
                y += 22;
                GUI.Label(new Rect(x, y, w, 20), $"FPS: {_currentFPS:F0}", _guiStyle);
            }
            else
            {
                GUI.Label(new Rect(x, y, w, 20), $"RECORDING: {configLabel}", _guiBoldStyle);
                y += 22;
                GUI.Label(new Rect(x, y, w, 20), $"FPS: {_currentFPS:F0}  |  Frames: {_recordedFrames}", _guiStyle);
                y += 20;

                float elapsed = Time.realtimeSinceStartup - _benchmarkStartTime;
                GUI.Label(new Rect(x, y, w, 20),
                    $"Time: {elapsed:F1}s / {benchmarkDuration:F0}s", _guiStyle);
                y += 22;

                GUI.DrawTexture(new Rect(x, y, w, 14), _bgTexture);
                GUI.color = new Color(0.2f, 0.85f, 0.3f);
                GUI.DrawTexture(new Rect(x, y, w * _progress, 14), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(x, y, w, 14), $"  {_progress * 100:F0}%",
                    new GUIStyle(_guiStyle) { fontSize = 10, alignment = TextAnchor.MiddleCenter });
            }
        }
    }
}