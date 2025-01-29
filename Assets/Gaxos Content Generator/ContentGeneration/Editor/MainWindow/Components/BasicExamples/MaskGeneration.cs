using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContentGeneration.Helpers;
using ContentGeneration.Models.Stability;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContentGeneration.Editor.MainWindow.Components.BasicExamples
{
    public class MaskGeneration : VisualElementComponent
    {
        public new class UxmlFactory : UxmlFactory<MaskGeneration, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }
        }
        
        TextField codeTextField => this.Q<TextField>("code");
        PromptInput prompt => this.Q<PromptInput>("promptInput");
        Label promptRequired => this.Q<Label>("promptRequiredLabel");
        ImageSelection maskImage => this.Q<ImageSelection>("mask");
        Label maskImageRequired => this.Q<Label>("maskImageRequiredLabel");
        Button generateButton => this.Q<Button>("generateButton");

        public MaskGeneration()
        {
            prompt.OnChanged += _ =>
            {
                RefreshCode();
            };
            maskImage.OnChanged += RefreshCode;
            RefreshCode();

            promptRequired.style.visibility = Visibility.Hidden;

            maskImageRequired.style.visibility = Visibility.Hidden;

            var sendingRequest = this.Q<VisualElement>("sendingRequest");
            var requestSent = this.Q<VisualElement>("requestSent");
            generateButton.RegisterCallback<ClickEvent>(_ =>
            {
                if (!generateButton.enabledSelf) return;

                requestSent.style.display = DisplayStyle.None;
                if (!IsValid())
                {
                    return;
                }

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
                                requestSent.style.display = DisplayStyle.Flex;
                            }

                            generateButton.SetEnabled(true);
                            sendingRequest.style.display = DisplayStyle.None;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }
                        ContentGenerationStore.Instance.RefreshRequestsAsync().Finally(() => ContentGenerationStore.Instance.RefreshStatsAsync().CatchAndLog());
                    });
            });
        }

        bool IsValid(bool updateUI = true)
        {
            promptRequired.style.visibility = Visibility.Hidden;
            maskImageRequired.style.visibility = Visibility.Hidden;
            if (string.IsNullOrWhiteSpace(prompt.value))
            {
                if(updateUI)
                {
                    promptRequired.style.visibility = Visibility.Visible;
                }
                return false;
            }

            if (maskImage.image == null)
            {
                if(updateUI)
                {
                    maskImageRequired.style.visibility = Visibility.Visible;
                }
                return false;
            }

            return true;
        }

        Task<string> RequestGeneration(bool estimate = false)
        {
            return ContentGenerationApi.Instance.RequestStabilityMaskedImageGeneration
            (new StabilityMaskedImageParameters
            {
                TextPrompts = new[]
                {
                    new Prompt
                    {
                        Text = prompt.value,
                        Weight = 1,
                    }
                },
                InitImage = (Texture2D)maskImage.image
            }, data: new
            {
                player_id = ContentGenerationStore.editorPlayerId
            }, estimate: estimate);
        }

        void RefreshCode()
        {
            codeTextField.value =
                "var requestId = await ContentGenerationApi.Instance.RequestMaskedImageGeneration\n" +
                "\t(new StabilityMaskedImageParameters\n" +
                "\t{\n" +
                "\t\tTextPrompts = new[]\n" +
                "\t\t{\n" +
                "\t\t\tnew Prompt\n" +
                "\t\t\t{\n" +
                $"\t\t\t\tText = \"{prompt.value}\",\n" +
                "\t\t\t\tWeight = 1,\n" +
                "\t\t\t},\n" +
                "\t\t\tMaskImage = <Texture2D object>\n" +
                "\t\t}\n" +
                "\t})";

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
    }
}