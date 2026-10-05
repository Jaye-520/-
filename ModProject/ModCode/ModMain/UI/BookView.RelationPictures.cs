using System;
using MOD_YtxSQ1.Services;
using UnityEngine;

namespace MOD_YtxSQ1.UI
{
    internal sealed partial class BookView
    {
        private sealed class RelationPictureJob
        {
            internal string Id;
            internal GameObject Root, Host;
            internal bool Ready, Cancelled;
            internal DateTime Started = DateTime.UtcNow;
        }
        private RelationPictureJob relationPictureJob;
        private void CompleteRelationPicture(RelationPictureJob job, GameObject root)
        {
            if (job.Cancelled || relationPictureJob != job)
            {
                if (root != null) Cleanup(() => UnityEngine.Object.Destroy(root), "过期小立绘模型");
                return;
            }
            job.Root = root; job.Ready = true;
        }
        private void CancelRelationPicture()
        {
            var job = relationPictureJob; relationPictureJob = null;
            if (job == null) return;
            job.Cancelled = true;
            if (job.Root != null) Cleanup(() => UnityEngine.Object.Destroy(job.Root), "未完成小立绘模型");
            ReleaseViewObject(job.Host, "未完成小立绘承载");
        }
        private bool RelationPictureVisible(string id)
        {
            Vector2 p;
            if (!relationPositions.TryGetValue(id, out p)) return false;
            float x = relationPage.anchoredPosition.x + p.x * relationZoom;
            float y = -relationPage.anchoredPosition.y + p.y * relationZoom;
            return x + 46 * relationZoom >= 0 && x - 46 * relationZoom <= relationViewport.sizeDelta.x &&
                y + 65 * relationZoom >= 0 && y - 65 * relationZoom <= relationViewport.sizeDelta.y;
        }
        private void TickRelationPictures()
        {
            if (relationGraph == null) return;
            // One transient static model at a time; cards retain only small textures, never live renderers.
            if (relationPictureJob != null && (!relationPictures.ContainsKey(relationPictureJob.Id) || !RelationPictureVisible(relationPictureJob.Id))) CancelRelationPicture();
            foreach (var entry in relationPictures)
            {
                if (relationTextures.ContainsKey(entry.Key) || relationPictureFailures.Contains(entry.Key) || !RelationPictureVisible(entry.Key)) continue;
                if (relationTextures.Count >= 128)
                {
                    string evict = null;
                    foreach (var cached in relationTextures.Keys) if (!RelationPictureVisible(cached)) { evict = cached; break; }
                    if (evict == null) return;
                    UnityEngine.Object.Destroy(relationTextures[evict]); relationTextures.Remove(evict);
                    UnityEngine.UI.RawImage previous;
                    if (relationPictures.TryGetValue(evict, out previous)) { previous.texture = null; previous.enabled = false; }
                }
                if (relationPictureJob != null && relationPictureJob.Id != entry.Key) continue;
                var id = entry.Key; var image = entry.Value;
                GameObject modelRoot = null, specialHost = null; ModelRenderTexture renderer = null; Texture2D texture = null;
                var active = RenderTexture.active;
                try
                {
                    if (relationPictureJob == null)
                    {
                        var job = new RelationPictureJob { Id = id };
                        relationPictureJob = job;
                        var onCreated = new Action<GameObject>(created => CompleteRelationPicture(job, created));
                        var unit = CharacterRelations.ResolveIncludingDead(id);
                        PortraitModel model = null;
                        if (unit != null)
                        {
                            int dramaId = CharacterPortrait.GetDramaId(unit);
                            if (dramaId > 0)
                            {
                                var conf = g.conf.dramaNpc.GetItem(dramaId);
                                if (conf != null && conf.avatar == "0" && conf.modelID != 0)
                                    model = PortraitModel.CreateModel(g.conf.dramaNpc.GetPortraitModelDataInModel(conf.modelID), false, false, onCreated);
                                else
                                {
                                    // Independent avatar/prefab uses the same native identity path as the shared portrait.
                                    var rect = NewRect("临时专属小立绘", content); job.Host = rect.gameObject;
                                    rect.sizeDelta = new Vector2(96, 112); rect.anchoredPosition = new Vector2(-4096, -4096);
                                    var nativeImage = job.Host.AddComponent<UnityEngine.UI.RawImage>();
                                    CharacterPortrait.CreateSpecialTexture(dramaId, nativeImage, onCreated);
                                }
                            }
                            else model = PortraitModel.CreateModel(unit, false, false, onCreated);
                        }
                        else
                        {
                            var dead = g.data.unitDie.GetUnitDie(id);
                            if (dead == null || dead.modelData == null) throw new InvalidOperationException("无人物立绘记录");
                            model = PortraitModel.CreateModel(dead.modelData, false, false, onCreated);
                        }
                        if (model != null) job.Root = model.root;
                        return;
                    }
                    if (!relationPictureJob.Ready)
                    {
                        if ((DateTime.UtcNow - relationPictureJob.Started).TotalSeconds > 10) throw new InvalidOperationException("人物立绘加载超时");
                        return;
                    }
                    modelRoot = relationPictureJob.Root; specialHost = relationPictureJob.Host;
                    relationPictureJob.Root = null; relationPictureJob.Host = null;
                    relationPictureJob = null;
                    if (modelRoot == null) throw new InvalidOperationException("静态小立绘未返回模型");
                    renderer = new ModelRenderTexture(modelRoot, 160, 154, Vector2.zero, image.transform);
                    var camera = renderer.camera; if (camera == null) throw new InvalidOperationException("静态小立绘相机未就绪");
                    // Native CreateRenderTex parents the model to its camera. Preserve world pose
                    // but detach before fitting, otherwise moving the camera also moves the subject.
                    modelRoot.transform.SetParent(null, true);
                    // Warm up mesh/skinning bounds before measuring the completed model.
                    camera.Render();
                    bool found = false; var bounds = new Bounds();
                    foreach (var part in modelRoot.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!part.enabled || !part.gameObject.activeInHierarchy || part.bounds.size.x <= .001f || part.bounds.size.y <= .001f) continue;
                        if (!found) { bounds = part.bounds; found = true; } else bounds.Encapsulate(part.bounds);
                    }
                    if (!found) throw new InvalidOperationException("静态小立绘边界未就绪");
                    camera.orthographic = true; camera.aspect = 80f / 77;
                    camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.12f;
                    camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, camera.transform.position.z);
                    camera.Render(); RenderTexture.active = camera.targetTexture;
                    texture = new Texture2D(160, 154, TextureFormat.RGBA32, false);
                    texture.ReadPixels(new Rect(0, 0, 160, 154), 0, 0); texture.Apply();
                    relationTextures[id] = texture; image.texture = texture; image.enabled = true; texture = null;
                }
                catch (Exception ex)
                {
                    CancelRelationPicture();
                    relationPictureFailures.Add(id);
                    if (image != null) Label(image.rectTransform, "立绘\n未读取", 0, 0, 80, 77, 13, Muted, TextAnchor.MiddleCenter);
                    Debug.LogWarning("[八荒秘录] 关系小立绘未读取 " + id + "：" + ex.Message);
                }
                finally
                {
                    RenderTexture.active = active;
                    if (renderer != null) Cleanup(renderer.Destroy, "小立绘临时渲染器");
                    if (modelRoot != null) Cleanup(() => UnityEngine.Object.Destroy(modelRoot), "小立绘临时模型");
                    ReleaseViewObject(specialHost, "小立绘临时专属承载");
                    if (texture != null) Cleanup(() => UnityEngine.Object.Destroy(texture), "失败的小立绘纹理");
                }
                return;
            }
        }
    }
}
