global using Vanara.Extensions;
global using Vanara.InteropServices;
global using Vanara.PInvoke;
global using static Vanara.PInvoke.D3D12;
global using static Vanara.PInvoke.D3DCompiler;
global using static Vanara.PInvoke.DXGI;
global using static Vanara.PInvoke.Kernel32;
global using static Vanara.PInvoke.User32;

DXSample sample = new D3D12HelloWindow(1280, 720, "D3D12 Hello Window");
Win32Application.Run(sample);