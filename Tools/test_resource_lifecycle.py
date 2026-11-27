#!/usr/bin/env python3
"""Run the ownership tests outside Unity; engine/backend behavior is deliberately excluded."""
from pathlib import Path
import os
import shutil
import subprocess
import tempfile

STUBS = 'namespace UnityEngine { public class Object {} public class GameObject : Object {} public class Transform : Object {} }\nnamespace LGameFramework.Resource { internal class AddressablesResourceBackend : IResourceBackend {\n public System.Threading.Tasks.Task InitializeAsync() => throw new System.NotSupportedException();\n public IResourceOperation<T> Load<T>(string a) where T : UnityEngine.Object => throw new System.NotSupportedException();\n public IResourceOperation<UnityEngine.GameObject> Instantiate(string a, UnityEngine.Transform p) => throw new System.NotSupportedException();\n public IResourceOperation<long> GetDownloadSize(string k) => throw new System.NotSupportedException();\n public IResourceOperation<object> DownloadDependencies(string k) => throw new System.NotSupportedException();\n} }'

root = Path(__file__).resolve().parents[1]
sdk = os.environ.get("LGAME_DOTNET") or shutil.which("dotnet")
if not sdk:
    raise SystemExit("Install .NET 8 SDK, or set LGAME_DOTNET to the dotnet executable.")
with tempfile.TemporaryDirectory(prefix="lgame-lifecycle-") as temp:
    work = Path(temp)
    framework = root / "Assets/LGameFramework"
    files = [f for f in (framework / "Runtime/Resource").glob("*.cs")
             if f.name != "AddressablesResourceBackend.cs"]
    files.append(framework / "Tests/EditMode/ResourceServiceTests.cs")
    from xml.sax.saxutils import escape
    includes = "".join(f'<Compile Include="{escape(str(f))}" />' for f in files)
    (work / "Tests.csproj").write_text(
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework>'
        '<IsTestProject>true</IsTestProject></PropertyGroup><ItemGroup>'
        '<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />'
        '<PackageReference Include="NUnit" Version="3.13.3" />'
        '<PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />'
        + includes + '</ItemGroup></Project>')
    (work / "UnityStubs.cs").write_text(STUBS)
    subprocess.run([sdk, "test", str(work / "Tests.csproj"), "--nologo", "--verbosity", "minimal"], check=True)
