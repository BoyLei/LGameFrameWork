using System;
using System.Threading.Tasks;
using UnityEngine;

namespace LGameFramework.Resource
{
    // 将引擎操作与所有权策略隔离，以便无需真实 catalog 就能测试生命周期。
    internal interface IResourceOperation<T> : IDisposable
    {
        Task<T> Completion { get; }
    }

    internal interface IResourceBackend
    {
        Task InitializeAsync();
        IResourceOperation<T> Load<T>(string address) where T : UnityEngine.Object;
        IResourceOperation<GameObject> Instantiate(string address, Transform parent);
        IResourceOperation<long> GetDownloadSize(string key);
        IResourceOperation<object> DownloadDependencies(string key);
    }
}
