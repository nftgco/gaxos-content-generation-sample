using System;
using System.Collections.Generic;
using System.Linq;
using ContentGeneration.Helpers;
using ContentGeneration.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContentGeneration.Editor.MainWindow.Components.RequestsList
{
    public class RequestedItemCommon : VisualElementComponent
    {
        public new class UxmlFactory : UxmlFactory<RequestedItemCommon, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            readonly UxmlBoolAttributeDescription _showImages = new()
            {
                name = "ShowImages",
                defaultValue = true
            };
            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield return new UxmlChildElementDescription(typeof(VisualElement)); }
            }
            
            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var element = (RequestedItemCommon)ve;
                element.showImages = _showImages.GetValueFromBag(bag, cc);
            }
        }

        SubWindow requestedItem => this.Q<SubWindow>("requestedItem");
        Label status => this.Q<Label>("status");
        Label generator => this.Q<Label>("generator");
        VisualElement errorDetails => this.Q<VisualElement>("errorDetails");
        Label error => this.Q<Label>("error");
        TextField generatorParameters => this.Q<TextField>("generatorParameters");
        TextField generatorResult => this.Q<TextField>("generatorResult");
        Button refreshButton => this.Q<Button>("refreshButton");
        Button deleteButton => this.Q<Button>("deleteButton");
        ScrollView imagesContainer => this.Q<ScrollView>("imagesContainer");
        Button saveFavorite => this.Q<Button>("saveFavorite");
        Button deleteFavorite => this.Q<Button>("deleteFavorite");

        bool _showImages;
        bool showImages
        {
            get => _showImages;
            set
            {
                _showImages = value;
                this.value = this.value;
            }
        }
        public override VisualElement contentContainer => this.Q<VisualElement>("childrenContainer");

        public RequestedItemCommon()
        {
            refreshButton.clicked += () =>
            {
                if (refreshButton.enabledSelf)
                {
                    Refresh();
                }
            };
            deleteButton.clicked += () =>
            {
                if (deleteButton.enabledSelf)
                {
                    deleteButton.SetEnabled(false);
                    ContentGenerationApi.Instance.DeleteRequest(value.ID).ContinueInMainThreadWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Debug.LogException(t.Exception!.InnerException);
                        }
                        else
                        {
                            OnDeleted?.Invoke();
                        }

                        deleteButton.SetEnabled(true);
                    });
                }
            };
            saveFavorite.clicked += () =>
            {
                if (saveFavorite.enabledSelf)
                {
                    refreshButton.SetEnabled(false);
                    ContentGenerationApi.Instance.AddFavorite(value.ID).Finally(() =>
                    {
                        ContentGenerationStore.Instance.RefreshFavoritesAsync().Finally(() =>
                        {
                            value = value;
                            refreshButton.SetEnabled(true);
                        });
                    });
                }
            };
            deleteFavorite.clicked += () =>
            {
                if (deleteFavorite.enabledSelf)
                {
                    refreshButton.SetEnabled(false);
                    ContentGenerationApi.Instance.DeleteFavorite(value.ID).Finally(() =>
                    {
                        ContentGenerationStore.Instance.RefreshFavoritesAsync().Finally(() =>
                        {
                            value = value;
                            refreshButton.SetEnabled(true);
                        });
                    });
                }
            };
            generatorParameters.SetVerticalScrollerVisibility(ScrollerVisibility.Auto);
            generatorResult.SetVerticalScrollerVisibility(ScrollerVisibility.Auto);
        }

        public void Refresh()
        {
            refreshButton.SetEnabled(false);
            ContentGenerationApi.Instance.GetRequest(value.ID).ContinueInMainThreadWith(t =>
            {
                if (t.IsFaulted)
                {
                    Debug.LogException(t.Exception!.InnerException);
                }
                else
                {
                    value = t.Result;
                    OnRefreshed?.Invoke(t.Result);
                }

                refreshButton.SetEnabled(true);
            });
        }

        public event Action OnDeleted;
        public event Action<Request> OnRefreshed;
        Request _value;

        public Request value
        {
            get => _value;
            set
            {
                _value = value;
                if (value == null)
                    return;

                var generatorName = value.Generator.ToString();
                if (generatorName.StartsWith("Stability"))
                {
                    requestedItem.subWindowIcon = "Stability AI";
                }
                else if (generatorName.StartsWith("Meshy"))
                {
                    requestedItem.subWindowIcon = "Meshy";
                }
                else if (generatorName.StartsWith("Gaxos"))
                {
                    requestedItem.subWindowIcon = "Gaxos Labs AI";
                }
                else
                {
                    requestedItem.subWindowIcon = null;
                }
                
                requestedItem.subWindowName = generatorName.CamelCaseToSpacesAndUpperCaseEachWord();

                status.text = value.Status.ToString();
                if (value.Status != RequestStatus.Generated)
                {
                    saveFavorite.style.display = DisplayStyle.None;
                    deleteFavorite.style.display = DisplayStyle.None;
                }
                else
                {
                    var isFavorite = ContentGenerationStore.Instance.Favorites.Any(i => i.ID == value.ID);
                    saveFavorite.style.display =  isFavorite ? DisplayStyle.None : DisplayStyle.Flex;
                    deleteFavorite.style.display =  isFavorite ? DisplayStyle.Flex : DisplayStyle.None;
                }

                refreshButton.style.display =
                    value.Status == RequestStatus.Pending ? DisplayStyle.Flex : DisplayStyle.None;
                
                status.ClearClassList();
                status.AddToClassList(status.text.ToLower());
                generator.text = value.Generator.ToString();

                errorDetails.style.display =
                    value.Status == RequestStatus.Failed ? DisplayStyle.Flex : DisplayStyle.None;
                error.text = value.GeneratorError?.Message +
                             (value.GeneratorError?.Error == null ? "" : $" [{value?.GeneratorError?.Error}]");

                generatorParameters.value = value.GeneratorParameters?.ToString();
                generatorResult.value = value.GeneratorResult?.ToString();

                imagesContainer.style.display = DisplayStyle.None;
                if (value is { Status: RequestStatus.Generated, Assets: not null } && showImages)
                {
                    var imagesToRemove = 
                        imagesContainer.Children()
                        .OfType<GeneratedImageElement>().ToList();

                    foreach (var image in value.Assets)
                    {
                        imagesContainer.style.display = DisplayStyle.Flex;
                        var existing = imagesToRemove.
                            FirstOrDefault(i => i.generatedAsset.ID == image.ID);
                        if(existing == null)
                        {
                            imagesContainer.Add(new GeneratedImageElement(image));
                        }
                        else
                        {
                            imagesToRemove.Remove(existing);
                            existing.Refresh();
                        }
                    }

                    foreach (var imageElement in imagesToRemove)
                    {
                        imagesContainer.Remove(imageElement);
                    }
                }
                else
                {
                    imagesContainer.Clear();
                }
            }
        }
    }
}