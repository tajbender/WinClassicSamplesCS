global using Vanara.Extensions;
global using Vanara.InteropServices;
global using Vanara.PInvoke;
global using static Vanara.PInvoke.D2d1;
global using static Vanara.PInvoke.D3D11;
global using static Vanara.PInvoke.D3D12;
global using static Vanara.PInvoke.D3DCompiler;
global using static Vanara.PInvoke.Dwrite;
global using static Vanara.PInvoke.DXC;
global using static Vanara.PInvoke.DXGI;
global using static Vanara.PInvoke.Kernel32;
global using static Vanara.PInvoke.User32;

DXSample sample = new D3D12Fullscreen(1280, 720, "D3D12 Fullscreen sample - Press the SPACE bar or ALT+Enter to toggle fullscreen mode and use the left and right arrow keys to change the rendering resolution");
return Win32Application.Run(sample);