using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;
using LGameFramework.Resource;
using NUnit.Framework;
using UnityEngine;

namespace LGameFramework.Tests
{
    public sealed class ResourceServiceTests
    {
        private SynchronizationContext previousContext;
        private OwnerContext context;
        private FakeBackend backend;
        private ResourceService service;

        [SetUp] public void SetUp()
        {
            previousContext = SynchronizationContext.Current;
            context = new OwnerContext();
            SynchronizationContext.SetSynchronizationContext(context);
            backend = new FakeBackend();
            service = new ResourceService(backend);
        }
        [TearDown] public void TearDown()
        {
            try { service.Dispose(); }
            finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
        }

        // Pump an owner-thread context just as Unity does, without blocking pending continuations.
        private void Finish(Task task)
        {
            var timeout = Stopwatch.StartNew();
            while (!task.IsCompleted)
            {
                context.Pump();
                if (timeout.ElapsedMilliseconds > 5000) Assert.Fail("Resource task did not complete.");
                Thread.Yield();
            }
            task.GetAwaiter().GetResult();
        }
        private T Finish<T>(Task<T> task) { Finish((Task)task); return task.GetAwaiter().GetResult(); }

        private sealed class OwnerContext : SynchronizationContext
        {
            private readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();
            public override void Post(SendOrPostCallback callback, object state) => queue.Enqueue(() => callback(state));
            public void Pump() { while (queue.TryDequeue(out var action)) action(); }
        }

        [Test] public void HandleReleasesExactlyOnceAndRejectsAccess()
        {
            var handle = Finish(service.LoadAsync<UnityEngine.Object>("asset"));
            handle.Dispose();
            handle.Dispose();
            service.Dispose();
            Assert.That(backend.Asset.Releases, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => { var value = handle.Asset; });
        }

        [Test] public void TwoLoadsOwnIndependentReferences()
        {
            var first = Finish(service.LoadAsync<UnityEngine.Object>("asset"));
            var firstOperation = backend.Asset;
            backend.Asset = new FakeOperation<UnityEngine.Object>(null);
            var second = Finish(service.LoadAsync<UnityEngine.Object>("asset"));
            first.Dispose();
            Assert.That(firstOperation.Releases, Is.EqualTo(1));
            Assert.That(second.IsReleased, Is.False);
            Assert.That(backend.Asset.Releases, Is.Zero);
        }

        [Test] public void FailedLoadReleasesOperation()
        {
            backend.Asset = FakeOperation<UnityEngine.Object>.Pending();
            var loading = service.LoadAsync<UnityEngine.Object>("missing");
            backend.Asset.Source.SetException(new InvalidOperationException("missing"));
            Assert.Throws<InvalidOperationException>(() => Finish(loading));
            Assert.That(backend.Asset.Releases, Is.EqualTo(1));
        }

        [Test] public void DisposeDuringLoadReleasesLateResult()
        {
            backend.Asset = FakeOperation<UnityEngine.Object>.Pending();
            var loading = service.LoadAsync<UnityEngine.Object>("asset");
            service.Dispose();
            backend.Asset.Source.SetResult(null);
            Assert.Throws<ObjectDisposedException>(() => Finish(loading));
            Assert.That(backend.Asset.Releases, Is.EqualTo(1));
        }

        [Test] public void ConcurrentInitializationIsShared()
        {
            var pending = new TaskCompletionSource<bool>();
            backend.Initialization = pending.Task;
            var first = service.InitializeAsync();
            var second = service.InitializeAsync();
            Assert.That(backend.Initializations, Is.EqualTo(1));
            pending.SetResult(true);
            Finish(first);
            Finish(second);
        }

        [Test] public void InitializationFailureCanRetryWithoutStartingLoad()
        {
            backend.Initialization = Task.FromException(new InvalidOperationException("init"));
            Assert.Throws<InvalidOperationException>(() => Finish(service.LoadAsync<UnityEngine.Object>("asset")));
            Assert.That(backend.Loads, Is.Zero);
            backend.Initialization = Task.CompletedTask;
            Finish(service.InitializeAsync());
            Assert.That(backend.Initializations, Is.EqualTo(2));
        }

        [Test] public void DisposeDuringInitializationDoesNotStartLoad()
        {
            var pending = new TaskCompletionSource<bool>();
            backend.Initialization = pending.Task;
            var loading = service.LoadAsync<UnityEngine.Object>("asset");
            service.Dispose();
            pending.SetResult(true);
            Assert.Throws<ObjectDisposedException>(() => Finish(loading));
            Assert.That(backend.Loads, Is.Zero);
        }

        [Test] public void ScopeReleasesLateArrivalAndOtherOwnedHandles()
        {
            var scope = new ResourceScope();
            var first = scope.Own(Finish(service.LoadAsync<UnityEngine.Object>("one")));
            scope.Dispose();
            scope.Dispose();
            Assert.That(first.IsReleased, Is.True);
            var late = Finish(service.LoadAsync<UnityEngine.Object>("two"));
            Assert.Throws<ObjectDisposedException>(() => scope.Own(late));
            Assert.That(late.IsReleased, Is.True);
        }

        [Test] public void InstanceIsReleasedWhenServiceCloses()
        {
            var handle = Finish(service.InstantiateAsync("prefab"));
            service.Dispose();
            handle.Dispose();
            Assert.That(backend.Instance.Releases, Is.EqualTo(1));
        }

        [Test] public void DownloadOperationsReleaseAfterSuccessAndFailure()
        {
            Assert.That(Finish(service.GetDownloadSizeAsync("label")), Is.EqualTo(42));
            Assert.That(backend.Size.Releases, Is.EqualTo(1));
            backend.Download = FakeOperation<object>.Pending();
            var downloading = service.DownloadDependenciesAsync("label");
            backend.Download.Source.SetException(new InvalidOperationException("offline"));
            Assert.Throws<InvalidOperationException>(() => Finish(downloading));
            Assert.That(backend.Download.Releases, Is.EqualTo(1));
        }

        [Test] public void ClosedServiceAndEmptyAddressesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => Finish(service.LoadAsync<UnityEngine.Object>(" ")));
            service.Dispose();
            Assert.Throws<ObjectDisposedException>(() => Finish(service.InstantiateAsync("prefab")));
            Assert.That(backend.Initializations, Is.Zero);
        }

        [Test] public void ScopeContinuesCleanupAfterOneReleaseThrows()
        {
            var releases = 0;
            var scope = new ResourceScope();
            scope.Own(new ResourceHandle<object>(null, () => releases++));
            scope.Own(new ResourceHandle<object>(null, () => throw new InvalidOperationException("release")));
            Assert.Throws<AggregateException>(() => scope.Dispose());
            Assert.That(releases, Is.EqualTo(1));
        }

        [Test] public void FailedInstanceReleasesOperation()
        {
            backend.Instance = FakeOperation<GameObject>.Pending();
            var loading = service.InstantiateAsync("missing");
            backend.Instance.Source.SetException(new InvalidOperationException("missing"));
            Assert.Throws<InvalidOperationException>(() => Finish(loading));
            Assert.That(backend.Instance.Releases, Is.EqualTo(1));
        }

        [Test] public void DownloadSuccessReleasesOperation()
        {
            Finish(service.DownloadDependenciesAsync("label"));
            Assert.That(backend.Download.Releases, Is.EqualTo(1));
        }

        [Test] public void SizeFailureReleasesOperation()
        {
            backend.Size = FakeOperation<long>.Pending();
            var query = service.GetDownloadSizeAsync("missing");
            backend.Size.Source.SetException(new InvalidOperationException("missing"));
            Assert.Throws<InvalidOperationException>(() => Finish(query));
            Assert.That(backend.Size.Releases, Is.EqualTo(1));
        }

        [Test] public void BackgroundReleaseIsRejectedWithoutLosingOwnership()
        {
            var handle = Finish(service.LoadAsync<UnityEngine.Object>("asset"));
            Task.Run(() => Assert.Throws<InvalidOperationException>(() => handle.Dispose())).GetAwaiter().GetResult();
            Assert.That(handle.IsReleased, Is.False);
            handle.Dispose();
            Assert.That(backend.Asset.Releases, Is.EqualTo(1));
        }

        private sealed class FakeOperation<T> : IResourceOperation<T>
        {
            public readonly TaskCompletionSource<T> Source = new TaskCompletionSource<T>();
            public Task<T> Completion => Source.Task;
            public int Releases;
            private FakeOperation() { }
            public FakeOperation(T value) { Source.SetResult(value); }
            public static FakeOperation<T> Pending() => new FakeOperation<T>();
            public void Dispose() => Releases++;
        }

        private sealed class FakeBackend : IResourceBackend
        {
            public Task Initialization = Task.CompletedTask;
            public int Initializations;
            public int Loads;
            public FakeOperation<UnityEngine.Object> Asset = new FakeOperation<UnityEngine.Object>(null);
            public FakeOperation<GameObject> Instance = new FakeOperation<GameObject>(null);
            public FakeOperation<long> Size = new FakeOperation<long>(42);
            public FakeOperation<object> Download = new FakeOperation<object>(null);
            public Task InitializeAsync() { Initializations++; return Initialization; }
            public IResourceOperation<T> Load<T>(string address) where T : UnityEngine.Object
            {
                Loads++;
                return (IResourceOperation<T>)(object)Asset;
            }
            public IResourceOperation<GameObject> Instantiate(string address, Transform parent) => Instance;
            public IResourceOperation<long> GetDownloadSize(string key) => Size;
            public IResourceOperation<object> DownloadDependencies(string key) => Download;
        }
    }
}
