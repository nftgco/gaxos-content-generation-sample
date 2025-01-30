using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ContentGeneration.Editor.MainWindow.Components.ElevenLabs;
using ContentGeneration.Editor.MainWindow.Components.Meshy;
using ContentGeneration.Editor.MainWindow.Components.StabilityAI;
using ContentGeneration.Helpers;
using ContentGeneration.Models;
using Unity.EditorCoroutines.Editor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using QueryParameters = ContentGeneration.Models.QueryParameters;

namespace ContentGeneration.Editor.MainWindow.Components.RequestsList
{
    public class RequestsListTab : VisualElementComponent
    {
        public new class UxmlFactory : UxmlFactory<RequestsListTab, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }
        }

        Button refreshButton => this.Q<Button>("refreshButton");
        MultiColumnListView listView => this.Q<MultiColumnListView>();
        IRequestedItem defaultRequestedItem => this.Q<RequestedItem>();

        IRequestedItem meshyTextToMeshRequestedItem => this.Q<MeshyTextToMeshRequestedItem>();
        IRequestedItem meshyTextToTextureRequestedItem => this.Q<MeshyTextToTextureRequestedItem>();
        IRequestedItem meshyTextToVoxelRequestedItem => this.Q<MeshyTextToVoxelRequestedItem>();
        IRequestedItem meshyImageToMeshRequestedItem => this.Q<MeshyImageToMeshRequestedItem>();
        IRequestedItem stabilityFast3dRequestedItem => this.Q<StabilityFast3dRequestedItem>();
        IRequestedItem elevenLabSoundRequestedItem => this.Q<ElevenLabSoundRequestedItem>();

        IRequestedItem[] allRequestedItems => new[]
        {
            defaultRequestedItem,
            meshyTextToMeshRequestedItem, meshyTextToTextureRequestedItem,
            meshyTextToVoxelRequestedItem, meshyImageToMeshRequestedItem,
            stabilityFast3dRequestedItem,
            elevenLabSoundRequestedItem
        };

        string _selectedId;

        public RequestsListTab()
        {
            refreshButton.RegisterCallback<ClickEvent>(_ => { Refresh(); });

            ContentGenerationStore.Instance.OnRequestsChanged += _ =>
            {
                var previousSelectId = _selectedId;
                listView.RefreshItems();
                listView.selectedIndex = -1;
                if (previousSelectId != null)
                {
                    for (var i = 0; i < ContentGenerationStore.Instance.Requests.Count; i++)
                    {
                        if (ContentGenerationStore.Instance.Requests[i].ID == previousSelectId)
                        {
                            listView.selectedIndex = i;
                            break;
                        }
                    }
                }
            };
            listView.itemsSource = ContentGenerationStore.Instance.Requests;
            if (!string.IsNullOrEmpty(Settings.instance.apiKey))
            {
                Refresh();
            }

            Func<VisualElement> CreateCell(int i1)
            {
                return () =>
                {
                    var ret = new Label
                    {
                        name = $"p{i1}"
                    };
                    ret.AddToClassList(ret.name);

                    return ret;
                };
            }

            for (var i = 0; i < listView.columns.Count; i++)
            {
                var listViewColumn = listView.columns[i];
                listViewColumn.makeCell = CreateCell(i);
            }

            listView.columnSortingChanged += Refresh;
            listView.columns["id"].bindCell = (element, index) =>
                (element as Label)!.text = ContentGenerationStore.Instance.Requests[index].ID.ToString() + $" - {index}";
            listView.columns["generator"].bindCell = (element, index) =>
                (element as Label)!.text = ContentGenerationStore.Instance.Requests[index].Generator.ToString();
            listView.columns["timeTaken"].bindCell = (element, index) =>
            {
                var completedAt = ContentGenerationStore.Instance.Requests[index].CompletedAt;
                var createdAt = ContentGenerationStore.Instance.Requests[index].CreatedAt;
                if (completedAt < createdAt)
                {
                    completedAt = DateTime.UtcNow;
                }

                (element as Label)!.text = $"{(completedAt - createdAt).TotalSeconds:0.} seconds";
            };
            listView.columns["created"].bindCell = (element, index) =>
                (element as Label)!.text = ContentGenerationStore.Instance.Requests[index].CreatedAt
                    .ToString(CultureInfo.InvariantCulture);
            listView.columns["completed"].bindCell = (element, index) =>
            {
                if (ContentGenerationStore.Instance.Requests[index].CompletedAt > DateTime.UnixEpoch)
                {
                    (element as Label)!.text = ContentGenerationStore.Instance.Requests[index].CompletedAt
                        .ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    (element as Label)!.text = "---";
                }
            };
            listView.columns["creditsCost"].bindCell = (element, index) =>
            {
                var label = (element as Label)!;
                label.text =
                    (ContentGenerationStore.Instance.Requests[index].DeductedSubscriptionCredits +
                     ContentGenerationStore.Instance.Requests[index].DeductedTopupCredits)
                    .ToString(CultureInfo.InvariantCulture);
            };
            listView.columns["status"].bindCell = (element, index) =>
            {
                var label = (element as Label)!;
                label.text = ContentGenerationStore.Instance.Requests[index].Status.ToString();
                label.RemoveFromClassList("generated");
                label.RemoveFromClassList("failed");
                label.AddToClassList(ContentGenerationStore.Instance.Requests[index].Status.ToString().ToLower());
            };
            listView.columns["download"].bindCell = (element, index) =>
            {
                element.RemoveFromClassList("downloadElement");
                element.AddToClassList("downloadElement");

                var button = element.Children().FirstOrDefault(c => c is Button) as Button;
                if (button != null)
                {
                    element.Remove(button);
                }

                button = new Button();
                element.Add(button);
                button.RemoveFromClassList("generated");
                button.RemoveFromClassList("failed");
                button.RemoveFromClassList("mdel3d");
                var request = ContentGenerationStore.Instance.Requests[index];
                if (request.Status == RequestStatus.Generated &&
                    request.Generator is Generator.MeshyTextToMesh or Generator.StabilityStableFast3d)
                {
                    button.AddToClassList("mdel3d");
                }

                button.AddToClassList(request.Status.ToString().ToLower());

                void DownloadButtonClicked()
                {
                    if (!button.enabledSelf)
                        return;

                    button.AddToClassList("disabled");
                    button.SetEnabled(false);
                    var downloadRequest = ContentGenerationStore.Instance.Requests[index];
                    if (downloadRequest.Generator is Generator.MeshyTextToMesh or Generator.StabilityStableFast3d)
                    {
                        (downloadRequest.Generator == Generator.MeshyTextToMesh
                                ? meshyTextToMeshRequestedItem
                                : stabilityFast3dRequestedItem)
                            .Save(downloadRequest)
                            .ContinueInMainThreadWith(t =>
                            {
                                if (t.IsFaulted)
                                {
                                    Debug.LogException(t.Exception!.InnerException);
                                }

                                button.RemoveFromClassList("disabled");
                                button.SetEnabled(true);
                            });
                    }
                    else if (downloadRequest.Generator is Generator.ElevenLabsSound or Generator.ElevenLabsTextToSpeech)
                    {
                        elevenLabSoundRequestedItem.Save(downloadRequest)
                            .ContinueInMainThreadWith(t =>
                            {
                                if (t.IsFaulted)
                                {
                                    Debug.LogException(t.Exception!.InnerException);
                                }

                                button.RemoveFromClassList("disabled");
                                button.SetEnabled(true);
                            });
                    }
                    else
                    {
                        SaveImagesAsync(downloadRequest.Assets.Select(i => i.URL).ToArray())
                            .ContinueInMainThreadWith(t =>
                            {
                                if (t.IsFaulted)
                                {
                                    Debug.LogException(t.Exception!.InnerException);
                                }
                                else
                                {
                                    GeneratedImageElement.SaveImageToProject(t.Result);
                                }

                                button.RemoveFromClassList("disabled");
                                button.SetEnabled(true);
                            });
                    }
                }

                button.clicked -= DownloadButtonClicked;
                if (request.Status == RequestStatus.Generated)
                {
                    button.clicked += DownloadButtonClicked;
                }
            };

            foreach (var requestedItem in allRequestedItems)
            {
                requestedItem.OnDeleted += () =>
                {
                    listView.selectedIndex = -1;
                    Refresh();
                };
                requestedItem.style.display = DisplayStyle.None;
            }

            listView.selectionChanged += objects =>
            {
                _selectedId = null;
                var objectsArray = objects.ToArray();
                foreach (var requestedItem in allRequestedItems)
                {
                    requestedItem.value = null;
                }

                if (objectsArray.Length > 0)
                {
                    var request = (objectsArray[0] as Request)!;
                    _selectedId = request.ID;
                    if (request.Generator == Generator.MeshyTextToMesh)
                    {
                        meshyTextToMeshRequestedItem.value = request;
                    }
                    else if (request.Generator == Generator.MeshyTextToTexture)
                    {
                        meshyTextToTextureRequestedItem.value = request;
                    }
                    else if (request.Generator == Generator.MeshyTextToVoxel)
                    {
                        meshyTextToVoxelRequestedItem.value = request;
                    }
                    else if (request.Generator == Generator.MeshyImageTo3d)
                    {
                        meshyImageToMeshRequestedItem.value = request;
                    }
                    else if (request.Generator == Generator.StabilityStableFast3d)
                    {
                        stabilityFast3dRequestedItem.value = request;
                    }
                    else if (request.Generator is Generator.ElevenLabsSound or Generator.ElevenLabsTextToSpeech)
                    {
                        elevenLabSoundRequestedItem.value = request;
                    }
                    else
                    {
                        defaultRequestedItem.value = request;
                    }
                }

                foreach (var requestedItem in allRequestedItems)
                {
                    requestedItem.style.display =
                        requestedItem.value == null ? DisplayStyle.None : DisplayStyle.Flex;
                }
            };
        }

        Task<Texture2D[]> SaveImagesAsync(string[] urls)
        {
            var ret = new TaskCompletionSource<Texture2D[]>();
            EditorCoroutineUtility.StartCoroutine(SaveImagesCo(urls, ret), this);
            return ret.Task;
        }

        IEnumerator SaveImagesCo(string[] urls, TaskCompletionSource<Texture2D[]> tcs)
        {
            var textures = new Texture2D[urls.Length];
            for (var i = 0; i < urls.Length; i++)
            {
                var www = UnityWebRequestTexture.GetTexture(urls[i]);
                www.SetRequestHeader("Authorization", $"Bearer {Settings.instance.apiKey}");
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    tcs.SetException(new Exception($"{www.error}: {www.downloadHandler?.text}"));
                    yield break;
                }

                try
                {
                    textures[i] = ((DownloadHandlerTexture)www.downloadHandler).texture;
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            }

            tcs.SetResult(textures);
        }

        void Refresh()
        {
            ContentGenerationStore.Instance.sortBy = null;

            if (listView.sortColumnDescriptions.Count > 0)
            {
                var sortColumnDescription = listView.sortColumnDescriptions[0];
                if (sortColumnDescription.columnName == "id")
                {
                    ContentGenerationStore.Instance.sortBy = new QueryParameters.SortBy
                    {
                        Target = QueryParameters.SortTarget.Id,
                        Direction = sortColumnDescription.direction == SortDirection.Ascending
                            ? QueryParameters.SortDirection.Ascending
                            : QueryParameters.SortDirection.Descending
                    };
                }
                else if (sortColumnDescription.columnName == "created")
                {
                    ContentGenerationStore.Instance.sortBy = new QueryParameters.SortBy
                    {
                        Target = QueryParameters.SortTarget.CreatedAt,
                        Direction = sortColumnDescription.direction == SortDirection.Ascending
                            ? QueryParameters.SortDirection.Ascending
                            : QueryParameters.SortDirection.Descending
                    };
                }
                else
                {
                    listView.sortColumnDescriptions.Clear();
                }
            }

            ContentGenerationStore.Instance.RefreshRequestsAsync().CatchAndLog();
        }
    }
}