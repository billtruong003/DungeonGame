namespace StylizedTextureBaker
{
    public static class StyleModuleFactory
    {
        public static IStyleModule Create(StyleType type)
        {
            switch (type)
            {
                case StyleType.Outline: return new OutlineStyle();
                case StyleType.CelShading: return new CelShadingStyle();
                case StyleType.Hatching: return new HatchingStyle();
                case StyleType.Painterly: return new PainterlyStyle();
                case StyleType.Weathering: return new WeatheringStyle();
                default: return null;
            }
        }

        public static IStyleModule CreateFromData(StyleLayerData data)
        {
            var module = Create(data.type);
            if (module == null) return null;

            module.Enabled = data.enabled;
            module.Order = data.order;
            module.BlendMode = data.blendMode;
            module.Opacity = data.opacity;
            module.Deserialize(data.serializedParameters);

            return module;
        }

        public static StyleLayerData ToData(IStyleModule module)
        {
            return new StyleLayerData
            {
                type = module.Type,
                enabled = module.Enabled,
                order = module.Order,
                blendMode = module.BlendMode,
                opacity = module.Opacity,
                serializedParameters = module.Serialize()
            };
        }
    }
}
