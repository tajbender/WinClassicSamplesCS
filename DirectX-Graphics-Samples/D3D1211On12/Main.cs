global using Vanara.Extensions;
global using Vanara.InteropServices;
global using Vanara.PInvoke;
global using static Vanara.PInvoke.D3D12;
global using static Vanara.PInvoke.D3DCompiler;
global using static Vanara.PInvoke.DXC;
global using static Vanara.PInvoke.DXGI;
global using static Vanara.PInvoke.Kernel32;
global using static Vanara.PInvoke.User32;

DXSample sample = new D3D1211on12(1280, 720, "D3D12 11 on 12 Sample");
Win32Application.Run(sample);