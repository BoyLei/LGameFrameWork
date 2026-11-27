using System;
using LGameFramework.Resource;
using UnityEngine;

namespace LGameFramework.Samples
{
    public sealed class ResourceDemo : MonoBehaviour
    {
        [SerializeField] private string prefabAddress = "demo/cube";
        private ResourceService resources;
        private ResourceScope scope;

        private async void Start()
        {
            resources = new ResourceService();
            scope = new ResourceScope();
            try
            {
                var instance = scope.Own(await resources.InstantiateAsync(prefabAddress, transform));
                Debug.Log($"Addressable instance loaded: {instance.Asset.name}", this);
            }
            catch (ObjectDisposedException) { /* 示例对象在加载完成前退出。 */ }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void OnDestroy()
        {
            scope?.Dispose();
            resources?.Dispose();
        }
    }
}
