using System;
using System.Threading.Tasks;
using ContentGeneration.Helpers;
using ContentGeneration.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContentGeneration.Editor.MainWindow.Components
{
    public interface IParameters<T>
    {
        GenerationOptionsElement generationOptions { get; }
        Action codeHasChanged { set; }
        bool Valid(bool updateUI = false);
        void ApplyParameters(T parameters);
        string GetCode();
        void Show(Favorite generatorParameters);
    }
    
    public abstract class ParametersBasedGenerator<T, TU> : VisualElementComponent, IGeneratorVisualElement 
        where T : VisualElement, IParameters<TU>
        where TU: new()
    {
        T parameters => this.Q<T>("parameters");

        TextField code => this.Q<TextField>("code");
        Button generateButton => this.Q<Button>("generate");
        VisualElement sendingRequest => this.Q<VisualElement>("sendingRequest");
        VisualElement requestSent => this.Q<VisualElement>("requestSent");
        VisualElement requestFailed => this.Q<VisualElement>("requestFailed");

        protected ParametersBasedGenerator()
        {
            parameters.codeHasChanged = RefreshCode;
            if(parameters.generationOptions!=null)
            {
                parameters.generationOptions.OnCodeHasChanged = RefreshCode;
            }

            sendingRequest.style.display = DisplayStyle.None;
            requestSent.style.display = DisplayStyle.None;
            requestFailed.style.display = DisplayStyle.None;

            generateButton.RegisterCallback<ClickEvent>(_ =>
            {
                if (!generateButton.enabledSelf) return;

                requestSent.style.display = DisplayStyle.None;
                requestFailed.style.display = DisplayStyle.None;
                if (!parameters.Valid())
                {
                    return;
                }

                generateButton.SetEnabled(false);
                sendingRequest.style.display = DisplayStyle.Flex;

                var requestParameters = new TU();
                parameters.ApplyParameters(requestParameters);
                RequestToApi(
                    requestParameters,
                    parameters.generationOptions?.GetGenerationOptions(), 
                    data: new
                    {
                        player_id = ContentGenerationStore.editorPlayerId
                    }).ContinueInMainThreadWith(
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

            code.SetVerticalScrollerVisibility(ScrollerVisibility.Auto);
            RefreshCode();
        }
        
        Task<string> RequestGeneration(bool estimate = false)
        {
            var requestParameters = new TU();
            parameters.ApplyParameters(requestParameters);
            return RequestToApi(
                requestParameters,
                parameters.generationOptions?.GetGenerationOptions(),
                new
                {
                    player_id = ContentGenerationStore.editorPlayerId
                }, estimate);
        }

        protected abstract Task<string> RequestToApi(TU parameters, 
            GenerationOptions generationOptions, 
            object data, bool estimate = false);

        protected abstract string apiMethodName { get; }
        void RefreshCode()
        {
            code.value =
                $"var requestId = await ContentGenerationApi.Instance.{apiMethodName}\n" +
                $"\t(new {typeof(TU).Name}\n" +
                "\t{\n" +
                parameters.GetCode() +
                "\t},\n" +
                parameters.generationOptions?.GetCode() +
                ")";

            if (parameters.Valid(false))
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

        public abstract Generator generator { get; }

        public void Show(Favorite favorite)
        {
            parameters.Show(favorite);
        }
    }
}