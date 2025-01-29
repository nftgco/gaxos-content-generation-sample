using System.Collections.Generic;
using System.Threading.Tasks;
using ContentGeneration.Helpers;
using ContentGeneration.Models;
using ContentGeneration.Models.DallE;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContentGeneration.Editor.MainWindow.Components.DallE
{
    public class InPainting : VisualElementComponent, IGeneratorVisualElement
    {
        public new class UxmlFactory : UxmlFactory<InPainting, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }
        }
        
        TextField code => this.Q<TextField>("code");
        DallEParametersElement dallEParametersElement => this.Q<DallEParametersElement>("dallEParametersElement");
        GenerationOptionsElement generationOptionsElement => this.Q<GenerationOptionsElement>("generationOptions");
        ImageSelection image => this.Q<ImageSelection>("image");
        Label imageRequired => this.Q<Label>("imageRequiredLabel");
        Label maskRequired => this.Q<Label>("maskRequiredLabel");
        ImageSelection mask => this.Q<ImageSelection>("mask");
        VisualElement requestSent => this.Q<VisualElement>("requestSent");
        VisualElement requestFailed => this.Q<VisualElement>("requestFailed");
        VisualElement sendingRequest => this.Q<VisualElement>("sendingRequest");
        Button generateButton => this.Q<Button>("generateButton");

        public InPainting()
        {
            dallEParametersElement.OnCodeChanged += RefreshCode;
            generationOptionsElement.OnCodeHasChanged = RefreshCode;
            image.OnChanged += RefreshCode;
            mask.OnChanged += RefreshCode;
            
            dallEParametersElement.model.value = Model.DallE2;
            dallEParametersElement.model.SetEnabled(false);

            imageRequired.style.visibility = Visibility.Hidden;
            maskRequired.style.visibility = Visibility.Hidden;
           
            requestSent.style.display = DisplayStyle.None;
            requestFailed.style.display = DisplayStyle.None;
            sendingRequest.style.display = DisplayStyle.None;

            generateButton.RegisterCallback<ClickEvent>(_ =>
            {
                if (!generateButton.enabledSelf) return;

                if (!IsValid(false)) return;

                generateButton.SetEnabled(false);
                sendingRequest.style.display = DisplayStyle.Flex;

                RequestGeneration().ContinueInMainThreadWith(
                    t =>
                    {
                        generateButton.SetEnabled(true);
                        sendingRequest.style.display = DisplayStyle.None;
                        if (t.IsFaulted)
                        {
                            requestFailed.style.display = DisplayStyle.Flex;
                            Debug.LogException(t.Exception);
                        }
                        else
                        {
                            requestSent.style.display = DisplayStyle.Flex;
                        }
                        ContentGenerationStore.Instance.RefreshRequestsAsync().Finally(() => ContentGenerationStore.Instance.RefreshStatsAsync().CatchAndLog());
                    });
            });

            RefreshCode();
        }

        Task<string> RequestGeneration(bool estimate=false)
        {
            var parameters = new DallEInpaintingParameters
            {
                Image = (Texture2D)image.image,
                Mask = (Texture2D)mask.image,
            };
            dallEParametersElement.ApplyParameters(parameters);
            var x = ContentGenerationApi.Instance.RequestDallEInpaintingGeneration(
                parameters,
                generationOptionsElement.GetGenerationOptions(), data: new
                {
                    player_id = ContentGenerationStore.editorPlayerId
                }, estimate: estimate);
            return x;
        }

        bool IsValid(bool updateUI)
        {
            if(updateUI)
            {
                requestSent.style.display = DisplayStyle.None;
                requestFailed.style.display = DisplayStyle.None;
                imageRequired.style.visibility = Visibility.Hidden;
                maskRequired.style.visibility = Visibility.Hidden;
            }

            if (!dallEParametersElement.Valid(updateUI))
            {
                return false;
            }

            if (image.image == null)
            {
                if (updateUI)
                {
                    imageRequired.style.visibility = Visibility.Visible;
                }

                return false;
            }

            if (mask.image == null)
            {
                if (updateUI)
                {
                    maskRequired.style.visibility = Visibility.Visible;
                }

                return false;
            }

            return true;
        }

        void RefreshCode()
        {
            code.value =
                "var requestId = await ContentGenerationApi.Instance.RequestDallETextToImageGeneration\n" +
                "\t(new DallETextToImageParameters\n" +
                "\t{\n" +
                dallEParametersElement?.GetCode() +
                "\t},\n" +
                $"{generationOptionsElement?.GetCode()}" +
                ")";

            if (IsValid(false))
            {
                generateButton.text = "Generate [...]";
                CostEstimation.WillRequestEstimation(() => RequestGeneration(true)).ContinueInMainThreadWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Debug.LogException(t.Exception!.GetBaseException());
                        return;
                    }

                    if(!string.IsNullOrEmpty(t.Result))
                        generateButton.text = $"Generate [estimated cost: {t.Result}]";
                });
            }
        }

        public Generator generator => Generator.DallEInpainting;
        public void Show(Favorite favorite)
        {
            var parameters = favorite.GeneratorParameters.ToObject<DallETextToImageParameters>();
            dallEParametersElement.Show(parameters);
            generationOptionsElement.Show(favorite.GenerationOptions);
            
            RefreshCode();
        }
    }
}