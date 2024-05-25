#if UNITY_6000
using Unity.Properties;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    [UxmlObject]
    public partial class VisibleBinding : CustomBinding, IDataSourceProvider
    {
        public VisibleBinding()
        {
            updateTrigger = BindingUpdateTrigger.OnSourceChanged;
        }

        public object dataSource => null;

        [CreateProperty] public PropertyPath dataSourcePath { get; set; }

        [UxmlAttribute("data-source-path")]
        public string DataSourcePathString
        {
            get => dataSourcePath.ToString();
            set => dataSourcePath = new PropertyPath(value);
        }

        protected override BindingResult Update(in BindingContext context)
        {
            var source = context.dataSource;
            var path = context.dataSourcePath;

            if (!PropertyContainer.TryGetValue(ref source, in path, out bool enabled))
            {
                return new BindingResult(BindingStatus.Failure, "Property not found");
            }

            context.targetElement.ShowVisualElement(enabled);
            context.targetElement.SetEnabled(enabled);
            return new BindingResult(BindingStatus.Success);
        }
    }
}
#endif