using UnityEngine;
using TMPro;

namespace RPGModular.UI
{
    /// <summary>
    /// Floating damage number. Spawn via Bill.Pool, tự return sau lifetime.
    /// Gắn lên prefab có TMP_Text component.
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private float lifetime = 1f;
        [SerializeField] private float floatSpeed = 1.5f;
        [SerializeField] private float fadeSpeed = 2f;

        private float timer;
        private Vector3 startPos;
        private Color startColor;

        public void Initialize(float damage, bool isCrit, Vector3 worldPos)
        {
            transform.position = worldPos + Vector3.up * 1.5f;
            startPos = transform.position;
            timer = 0f;

            if (text == null) text = GetComponent<TMP_Text>();
            if (text == null) return;

            text.text = Mathf.RoundToInt(damage).ToString();

            if (isCrit)
            {
                text.fontSize = 8;
                text.color = new Color(1f, 0.8f, 0.1f, 1f);
            }
            else
            {
                text.fontSize = 5;
                text.color = Color.white;
            }

            startColor = text.color;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float t = timer / lifetime;

            transform.position = startPos + Vector3.up * (floatSpeed * t);

            if (text != null)
            {
                var c = startColor;
                c.a = Mathf.Lerp(1f, 0f, Mathf.Max(0, (t - 0.5f) * fadeSpeed));
                text.color = c;
            }

            if (timer >= lifetime)
            {
                var pooled = GetComponent<BillGameCore.PooledObject>();
                if (pooled != null)
                    pooled.ReturnToPool();
                else
                    Destroy(gameObject);
            }
        }
    }
}
