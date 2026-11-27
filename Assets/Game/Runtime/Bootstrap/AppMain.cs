using LGameFramework.Resource;
using UnityEngine;

// 保留现有类名和脚本 GUID，场景中已有的引用无需重绑。
public sealed class AppMain : MonoBehaviour
{
    public IResourceService Resources { get; private set; }
    private void Awake() => Resources = new ResourceService();
    // 按需初始化：项目尚未创建 Addressables Settings 时不在空场景启动加载。
    private void OnDestroy() => Resources?.Dispose();
}
