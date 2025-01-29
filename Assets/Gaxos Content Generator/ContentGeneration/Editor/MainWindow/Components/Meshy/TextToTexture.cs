using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ContentGeneration.Helpers;
using ContentGeneration.Models;
using ContentGeneration.Models.Meshy;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Resolution = ContentGeneration.Models.Meshy.Resolution;

namespace ContentGeneration.Editor.MainWindow.Components.Meshy
{
    public class TextToTexture : VisualElementComponent, IGeneratorVisualElement
    {
        public new class UxmlFactory : UxmlFactory<TextToTexture, UxmlTraits>
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
        GenerationOptionsElement generationOptionsElement => this.Q<GenerationOptionsElement>("generationOptions");
        VisualElement requestSent => this.Q<VisualElement>("requestSent");
        VisualElement requestFailed => this.Q<VisualElement>("requestFailed");
        VisualElement sendingRequest => this.Q<VisualElement>("sendingRequest");
        Button generateButton => this.Q<Button>("generateButton");

        ObjectField model => this.Q<ObjectField>("model");
        VisualElement modelRequired => this.Q<VisualElement>("modelRequired");
        PromptInput objectPrompt => this.Q<PromptInput>("objectPrompt");
        VisualElement objectPromptRequired => this.Q<VisualElement>("objectPromptRequired");
        PromptInput stylePrompt => this.Q<PromptInput>("stylePrompt");
        VisualElement stylePromptRequired => this.Q<VisualElement>("stylePromptRequired");

        PromptInput negativePrompt => this.Q<PromptInput>("negativePrompt");
        Toggle enableOriginalUv => this.Q<Toggle>("enableOriginalUv");
        Toggle enablePbr => this.Q<Toggle>("enablePbr");
        EnumField resolution => this.Q<EnumField>("resolution");

        EnumField artStyle => this.Q<EnumField>("artStyle");

        byte[] _modelBytes;
        string _modelExtension;

        Button improvePrompt => this.Q<Button>("improvePromptButton");

        public TextToTexture()
        {
            generationOptionsElement.OnCodeHasChanged = RefreshCode;

            model.RegisterValueChangedCallback(v =>
            {
                _modelBytes = null;
                _modelExtension = null;
                if (v.newValue != null)
                {
                    var path = AssetDatabase.GetAssetPath(v.newValue);
                    _modelBytes = File.ReadAllBytes(path);
                    _modelExtension = Path.GetExtension(path).TrimStart('.');
                }

                RefreshCode();
            });

            objectPrompt.OnChanged += _ => RefreshCode();
            stylePrompt.OnChanged += _ => RefreshCode();

            negativePrompt.OnChanged += _ => RefreshCode();

            enableOriginalUv.RegisterValueChangedCallback(_ => RefreshCode());
            enablePbr.RegisterValueChangedCallback(_ => RefreshCode());
            resolution.RegisterValueChangedCallback(_ => RefreshCode());

            artStyle.RegisterValueChangedCallback(_ => RefreshCode());

            requestSent.style.display = DisplayStyle.None;
            requestFailed.style.display = DisplayStyle.None;
            sendingRequest.style.display = DisplayStyle.None;

            improvePrompt.clicked += () =>
            {
                if (string.IsNullOrEmpty(stylePrompt.value))
                    return;

                if (!improvePrompt.enabledSelf)
                    return;

                improvePrompt.SetEnabled(false);
                stylePrompt.SetEnabled(false);

                ContentGenerationApi.Instance.ImprovePrompt(stylePrompt.value, "dalle-3").ContinueInMainThreadWith(
                    t =>
                    {
                        improvePrompt.SetEnabled(true);
                        stylePrompt.SetEnabled(true);
                        if (t.IsFaulted)
                        {
                            Debug.LogException(t.Exception!.InnerException!);
                            return;
                        }

                        stylePrompt.value = t.Result;
                    });
            };

            generateButton.RegisterCallback<ClickEvent>(_ =>
            {
                if (!generateButton.enabledSelf) return;

                if (!IsValid(true)) return;

                requestSent.style.display = DisplayStyle.None;
                requestFailed.style.display = DisplayStyle.None;

                generateButton.SetEnabled(false);
                sendingRequest.style.display = DisplayStyle.Flex;

                RequestGeneration(false).ContinueInMainThreadWith(
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

                        ContentGenerationStore.Instance.RefreshRequestsAsync().Finally(() =>
                            ContentGenerationStore.Instance.RefreshStatsAsync().CatchAndLog());
                    });
            });

            RefreshCode();
        }

        Task<string> RequestGeneration(bool estimate)
        {
            var parameters = new MeshyTextToTextureParameters
            {
                Model = _modelBytes,
                ModelExtension = _modelExtension,
                ObjectPrompt = objectPrompt.value,
                StylePrompt = stylePrompt.value,
                NegativePrompt = string.IsNullOrEmpty(negativePrompt.value) ? null : negativePrompt.value,
                EnableOriginalUV = enableOriginalUv.value,
                EnablePbr = enablePbr.value,
                Resolution = (Resolution)resolution.value,
                ArtStyle = (TextToTextureArtStyle)artStyle.value
            };
            return ContentGenerationApi.Instance.RequestMeshyTextToTextureGeneration(
                parameters,
                generationOptionsElement.GetGenerationOptions(), data: new
                {
                    player_id = ContentGenerationStore.editorPlayerId
                }, estimate: estimate);
        }

        bool IsValid(bool updateUI)
        {
            if (updateUI)
            {
                modelRequired.style.visibility = Visibility.Hidden;
                objectPromptRequired.style.visibility = Visibility.Hidden;
                stylePromptRequired.style.visibility = Visibility.Hidden;
            }

            if (_modelBytes == null)
            {
                if (updateUI)
                    modelRequired.style.visibility = Visibility.Visible;
                return false;
            }

            if (string.IsNullOrEmpty(objectPrompt.value))
            {
                if (updateUI)
                    objectPromptRequired.style.visibility = Visibility.Visible;
                return false;
            }

            if (string.IsNullOrEmpty(stylePrompt.value))
            {
                if (updateUI)
                    stylePromptRequired.style.visibility = Visibility.Visible;
                return false;
            }

            return true;
        }

        void RefreshCode()
        {
            code.value =
                "var requestId = await ContentGenerationApi.Instance.RequestMeshyTextToTextureGeneration\n" +
                "\t(new MeshyTextToTextureParameters\n" +
                "\t{\n" +
                $"\t\tModel = <Model bytes>,\n" +
                $"\t\tModel = \"{_modelExtension}\",\n" +
                $"\t\tObjectPrompt = \"{objectPrompt.value}\",\n" +
                $"\t\tStylePrompt = \"{stylePrompt.value}\",\n" +
                (string.IsNullOrEmpty(negativePrompt.value)
                    ? ""
                    : $"\t\tNegativePrompt = \"{negativePrompt.value}\",\n") +
                $"\t\tEnableOriginalUV = {enableOriginalUv.value},\n" +
                $"\t\tEnablePbr = {enablePbr.value},\n" +
                $"\t\tResolution = Resolution.{resolution.value},\n" +
                $"\t\tArtStyle = ArtStyle.{artStyle.value}\n" +
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

        public Generator generator => Generator.MeshyTextToTexture;

        public void Show(Favorite favorite)
        {
            var parameters = favorite.GeneratorParameters.ToObject<MeshyTextToTextureParameters>();
            generationOptionsElement.Show(favorite.GenerationOptions);

            objectPrompt.value = parameters.ObjectPrompt;
            stylePrompt.value = parameters.StylePrompt;
            negativePrompt.value = parameters.NegativePrompt;
            enableOriginalUv.value = parameters.EnableOriginalUV;
            enablePbr.value = parameters.EnablePbr;
            resolution.value = parameters.Resolution;
            artStyle.value = parameters.ArtStyle;

            RefreshCode();
        }
    }
}