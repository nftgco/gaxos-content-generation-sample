using System.Collections.Generic;
using System.Threading.Tasks;
using ContentGeneration.Helpers;
using ContentGeneration.Models.Stability;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContentGeneration.Editor.MainWindow.Components.BasicExamples
{
    public class TextToImage : VisualElementComponent
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

        TextField codeTextField => this.Q<TextField>("code");
        PromptInput prompt => this.Q<PromptInput>("promptInput");
        Label promptRequired => this.Q<Label>("promptRequiredLabel");
        Button generateButton => this.Q<Button>("generateButton");

        public TextToImage()
        {
            prompt.OnChanged += _ => { RefreshCode(); };
            RefreshCode();

            promptRequired.style.visibility = Visibility.Hidden;

            var sendingRequest = this.Q<VisualElement>("sendingRequest");
            sendingRequest.style.display = DisplayStyle.None;
            var requestSent = this.Q<VisualElement>("requestSent");
            requestSent.style.display = DisplayStyle.None;
            var requestFailed = this.Q<VisualElement>("requestFailed");
            requestFailed.style.display = DisplayStyle.None;

            generateButton.RegisterCallback<ClickEvent>(_ =>
            {
                if (!generateButton.enabledSelf) return;

                requestSent.style.display = DisplayStyle.None;
                requestFailed.style.display = DisplayStyle.None;

                if (!IsValid())
                {
                    return;
                }

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
                            prompt.value = null;
                            requestSent.style.display = DisplayStyle.Flex;
                        }

                        ContentGenerationStore.Instance.RefreshRequestsAsync().Finally(() =>
                            ContentGenerationStore.Instance.RefreshStatsAsync().CatchAndLog());
                    });
            });
        }

        bool IsValid(bool updateUI = true)
        {
            promptRequired.style.visibility = Visibility.Hidden;
            if (string.IsNullOrWhiteSpace(prompt.value))
            {
                if (updateUI)
                {
                    promptRequired.style.visibility = Visibility.Visible;
                }

                return false;
            }

            return true;
        }

        Task<string> RequestGeneration(bool estimate = false)
        {
            return ContentGenerationApi.Instance.RequestStabilityTextToImageGeneration
            (new StabilityTextToImageParameters
            {
                TextPrompts = new[]
                {
                    new Prompt
                    {
                        Text = prompt.value,
                        Weight = 1,
                    }
                },
            }, data: new
            {
                player_id = ContentGenerationStore.editorPlayerId
            }, estimate: estimate);
        }

        void RefreshCode()
        {
            codeTextField.value =
                "var requestId = await ContentGenerationApi.Instance.RequestGeneration\n" +
                "\t(new StabilityTextToImageParameters\n" +
                "\t{\n" +
                "\t\tTextPrompts = new[]\n" +
                "\t\t{\n" +
                "\t\t\tnew Prompt\n" +
                "\t\t\t{\n" +
                $"\t\t\t\tText = \"{prompt.value}\",\n" +
                "\t\t\t\tWeight = 1,\n" +
                "\t\t\t}\n" +
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