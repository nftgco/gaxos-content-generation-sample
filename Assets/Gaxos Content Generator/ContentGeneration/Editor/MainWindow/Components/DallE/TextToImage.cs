using System.Collections.Generic;
using System.Threading.Tasks;
using ContentGeneration.Models;
using ContentGeneration.Models.DallE;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace ContentGeneration.Editor.MainWindow.Components.DallE
{
    public class TextToImage : ParametersBasedGenerator<TextToImageParameters, DallETextToImageParameters>
    {
        public new class UxmlFactory : UxmlFactory<TextToImage, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }
        }

        protected override string apiMethodName => nameof(ContentGenerationApi.RequestDallETextToImageGeneration);
        protected override Task RequestToApi(DallETextToImageParameters parameters, GenerationOptions generationOptions, object data)
        {
            return ContentGenerationApi.Instance.RequestDallETextToImageGeneration(
                    parameters,
                    generationOptions, 
                    data: data);
        }

        public override Generator generator => Generator.DallETextToImage;
        public override void Show(JObject generatorParameters)
        {
        }
    }
}