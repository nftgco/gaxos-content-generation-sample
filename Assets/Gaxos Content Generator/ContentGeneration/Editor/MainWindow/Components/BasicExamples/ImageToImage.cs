using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContentGeneration.Helpers;
using ContentGeneration.Models.Stability;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContentGeneration.Editor.MainWindow.Components.BasicExamples
{
    public class ImageToImage : VisualElementComponent
    {
        public new class UxmlFactory : UxmlFactory<ImageToImage, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }
        }

        PromptInput prompt => this.Q<PromptInput>("promptInput");
        ImageSelection image => this.Q<ImageSelection>("image");
        Button generateButton => this.Q<Button>("generateButton");

        Label promptRequired => this.Q<Label>("promptRequiredLabel");
        Label imageRequired => this.Q<Label>("imageRequiredLabel");
        TextField codeTextField => this.Q<TextField>("code");

        public ImageToImage()
        {
            prompt.OnChanged += _ => RefreshCode();
            image.OnChanged += RefreshCode;

            promptRequired.style.visibility = Visibility.Hidden;
            imageRequired.style.visibility = Visibility.Hidden;

            var sendingRequest = this.Q<VisualElement>("sendingRequest");
            var requestSent = this.Q<VisualElement>("requestSent");
            generateButton.RegisterCallback<ClickEvent>(_ =>
            {
                if (!generateButton.enabledSelf) return;

                requestSent.style.display = DisplayStyle.None;
                
                if (!IsValid()) return;

                generateButton.SetEnabled(false);
                sendingRequest.style.display = DisplayStyle.Flex;

                RequestGeneration().ContinueInMainThreadWith(
                    t =>
                    {
                        try
                        {
                            if (t.IsFaulted)
                            {
                                Debug.LogException(t.Exception);
                            }
                            else
                            {
                                prompt.value = null;
                                image.image = null;
                                requestSent.style.display = DisplayStyle.Flex;
                            }

                            generateButton.SetEnabled(true);
                            sendingRequest.style.display = DisplayStyle.None;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }

                        ContentGenerationStore.Instance.RefreshRequestsAsync().Finally(() =>
                            ContentGenerationStore.Instance.RefreshStatsAsync().CatchAndLog());
                    });
            });
            RefreshCode();
        }

        bool IsValid(bool updateUI = true)
        {
            promptRequired.style.visibility = Visibility.Hidden;
            imageRequired.style.visibility = Visibility.Hidden;
            if (string.IsNullOrWhiteSpace(prompt.value))
            {
                if(updateUI)
                {
                    promptRequired.style.visibility = Visibility.Visible;
                }
                return false;
            }

            if (image.image == null)
            {
                if(updateUI)
                {
                    imageRequired.style.visibility = Visibility.Visible;
                }
                return false;
            }

            return true;
        }

        Task<string> RequestGeneration(bool estimate = false)
        {
            return ContentGenerationApi.Instance.RequestStabilityImageToImageGeneration
            (new StabilityImageToImageParameters
            {
                TextPrompts = new[]
                {
                    new Prompt
                    {
                        Text = prompt.value,
                        Weight = 1,
                    }
                },
                InitImage = (Texture2D)image.image
            }, data: new
            {
                player_id = ContentGenerationStore.editorPlayerId
            }, estimate: estimate);
        }

        void RefreshCode()
        {
            codeTextField.value =
                "var requestId = await ContentGenerationApi.Instance.RequestImageToImageGeneration\n" +
                "\t(new StabilityImageToImageParameters\n" +
                "\t{\n" +
                "\t\tTextPrompts = new[]\n" +
                "\t\t{\n" +
                "\t\t\tnew Prompt\n" +
                "\t\t\t{\n" +
                $"\t\t\t\tText = \"{prompt.value}\",\n" +
                "\t\t\t\tWeight = 1,\n" +
                "\t\t\t},\n" +
                "\t\t\tInitImage = <Texture2D object>\n" +
                "\t\t}\n" +
                "\t})";

            if(IsValid(false))
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
    }
}