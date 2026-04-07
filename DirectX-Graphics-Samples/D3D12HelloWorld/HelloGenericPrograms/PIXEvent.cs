using static Vanara.PInvoke.D3D11;

/// <summary>A self closing PIX event that starts and ends a user-defined event for a timing capture of CPU activity.</summary>
/// <example>
/// <code language="cs">using (PIXEvent evt = new(m_commandQueue!, "Render 3D"))
///{
///&nbsp;&nbsp;&nbsp;// Record all the commands we need to render the scene
///&nbsp;&nbsp;&nbsp;PopulateCommandList();
///
///&nbsp;&nbsp;&nbsp;// Execute the command list
///&nbsp;&nbsp;&nbsp;m_commandQueue!.ExecuteCommandLists(1, [m_commandList!]);
///}</code>
/// </example>
/// <seealso cref="System.IDisposable"/>
public class PIXEvent : IDisposable
{
	/// <summary>The i unk</summary>
	private readonly object iUnk;

	/// <summary>Initializes a new instance of the <see cref="PIXEvent"/> class.</summary>
	/// <param name="context">The context for the event.</param>
	/// <param name="pFormat">
	/// The name to use to describe the event, as a string. The string may specify zero or more optional string format placeholders in <see
	/// cref="string.Format(string, object?[])"/> style.
	/// </param>
	/// <param name="args">
	/// If placeholders are used in <paramref name="pFormat"/>, there must be a corresponding number of parameters whose types depend on the placeholders.
	/// </param>
	public PIXEvent(ID3D12CommandQueue context, string pFormat, params object?[] args)
	{
		iUnk = context;
		PIXBeginEvent(context, 0, pFormat, args);
	}

	/// <summary>Initializes a new instance of the <see cref="PIXEvent"/> class.</summary>
	/// <param name="context">The context for the event.</param>
	/// <param name="pFormat">
	/// The name to use to describe the event, as a string. The string may specify zero or more optional string format placeholders in <see
	/// cref="string.Format(string, object?[])"/> style.
	/// </param>
	/// <param name="args">
	/// If placeholders are used in <paramref name="pFormat"/>, there must be a corresponding number of parameters whose types depend on the placeholders.
	/// </param>
	public PIXEvent(ID3D12GraphicsCommandList context, string pFormat, params object?[] args)
	{
		iUnk = context;
		PIXBeginEvent(context, 0, pFormat, args);
	}

	/// <summary>Initializes a new instance of the <see cref="PIXEvent"/> class.</summary>
	/// <param name="context">The context for the event.</param>
	/// <param name="pFormat">
	/// The name to use to describe the event, as a string. The string may specify zero or more optional string format placeholders in <see
	/// cref="string.Format(string, object?[])"/> style.
	/// </param>
	/// <param name="args">
	/// If placeholders are used in <paramref name="pFormat"/>, there must be a corresponding number of parameters whose types depend on the placeholders.
	/// </param>
	public PIXEvent(ID3DUserDefinedAnnotation context, string pFormat, params object?[] args)
	{
		iUnk = context;
		PIXBeginEvent(context, 0, pFormat, args);
	}

	/// <summary>Initializes a new instance of the <see cref="PIXEvent"/> class.</summary>
	/// <param name="context">The context for the event.</param>
	/// <param name="pFormat">
	/// The name to use to describe the event, as a string. The string may specify zero or more optional string format placeholders in <see
	/// cref="string.Format(string, object?[])"/> style.
	/// </param>
	/// <param name="args">
	/// If placeholders are used in <paramref name="pFormat"/>, there must be a corresponding number of parameters whose types depend on the placeholders.
	/// </param>
	public PIXEvent(ID3D11DeviceContext2 context, string pFormat, params object?[] args)
	{
		iUnk = context;
		PIXBeginEvent(context, 0, pFormat, args);
	}

	/// <summary>
	/// Starts a user-defined event for a timing capture of CPU activity, to be displayed in the <b>System Timing Capture</b> feature of PIX.
	/// </summary>
	/// <param name="context">
	/// Context for the event, accepts <c>ID3D12GraphicsCommandList\*</c>, <c>ID3D12GraphicsCommandList\*</c> and
	/// <c>ID3D12XboxDmaCommandList\*</c> (Xbox only).
	/// </param>
	/// <param name="color">
	/// The event color to use in the system timing chart. Use <c>PIX_COLOR</c> to specify a color, <c>PIX_COLOR_INDEX</c> to specify a
	/// color index, or pass in a raw DWORD noting that the format is ARGB and the alpha channel value must be 0xff.
	/// </param>
	/// <param name="formatString">
	/// The name to use to describe the event, as a pointer to a null-terminated Unicode string. The string may specify zero or more
	/// optional string format placeholders, very similar to <c>sprintf</c> formatting.
	/// </param>
	/// <param name="args">
	/// If placeholders are used in formatString, there must be a corresponding number of parameters whose types depend on the placeholders.
	/// This method supports up to a maximum of 16 format parameters.
	/// </param>
	/// <remarks>
	/// <para>
	/// The <c>PIXBeginEvent</c> function saves format string and format parameters instead of formatting the string at runtime. Formatting
	/// is then done when reading capture files in PIX. Use 16-byte aligned strings (preferable) or 8-byte aligned strings with
	/// <c>PIXBeginEvent</c> to get the best performance. To print a char* or wchar_t* as a pointer using %p format specifier, cast the
	/// pointer to void* or a pointer to an integral or a floating point type when passing it to <c>PIXBeginEvent</c>. For the best
	/// performance, use a statically allocated string.
	/// </para>
	/// <para>
	/// Calls to <c>PIXBeginEvent</c> are guaranteed at least 512 bytes of space to save the record data, which includes the full size and
	/// alignment of the format string and all variables. In general, PIX events are intended for short high-performance markers that align
	/// to your game's major components, systems, or content.
	/// </para>
	/// <para>
	/// This method is used to time CPU events. For more information about timing GPU events, see the <c>PIX GPU Capture APIs</c> section of <c>PIX3</c>.
	/// </para>
	/// <para>
	/// Each call to <c>PIXBeginEvent</c> must have a matching call to <c>PIXEndEvent</c>. The paired calls to <c>PIXBeginEvent</c> and
	/// <c>PIXEndEvent</c> must occur on the same thread. The timing interval is about 200ns, and there is a low overhead to using this
	/// function, so up to several hundred thousand calls to <c>PIXBeginEvent</c> can be made per second.
	/// </para>
	/// <para><c>PIXBeginEvent</c> and <c>PIXEndEvent</c> pairs can be nested to any depth.</para>
	/// </remarks>
	// https://learn.microsoft.com/en-us/gaming/gdk/_content/gc/reference/tools/pix3/functions/pixbeginevent_4 void PIXBeginEvent( void*
	// context, UINT64 color, PCWSTR formatString, ... )
	[PInvokeData("pix_win.h")]
	public static void PIXBeginEvent(ID3D12CommandQueue context, ulong color, string formatString, params object?[] args)
	{
		using SafeLPWSTR str = new(string.Format(formatString, args));
		context.BeginEvent(0, str, str.Size);
	}

	/// <summary>
	/// Starts a user-defined event for a timing capture of CPU activity, to be displayed in the <b>System Timing Capture</b> feature of PIX.
	/// </summary>
	/// <param name="context">
	/// Context for the event, accepts <c>ID3D12GraphicsCommandList\*</c>, <c>ID3D12GraphicsCommandList\*</c> and
	/// <c>ID3D12XboxDmaCommandList\*</c> (Xbox only).
	/// </param>
	/// <param name="color">
	/// The event color to use in the system timing chart. Use <c>PIX_COLOR</c> to specify a color, <c>PIX_COLOR_INDEX</c> to specify a
	/// color index, or pass in a raw DWORD noting that the format is ARGB and the alpha channel value must be 0xff.
	/// </param>
	/// <param name="formatString">
	/// The name to use to describe the event, as a pointer to a null-terminated Unicode string. The string may specify zero or more
	/// optional string format placeholders, very similar to <c>sprintf</c> formatting.
	/// </param>
	/// <param name="args">
	/// If placeholders are used in formatString, there must be a corresponding number of parameters whose types depend on the placeholders.
	/// This method supports up to a maximum of 16 format parameters.
	/// </param>
	/// <remarks>
	/// <para>
	/// The <c>PIXBeginEvent</c> function saves format string and format parameters instead of formatting the string at runtime. Formatting
	/// is then done when reading capture files in PIX. Use 16-byte aligned strings (preferable) or 8-byte aligned strings with
	/// <c>PIXBeginEvent</c> to get the best performance. To print a char* or wchar_t* as a pointer using %p format specifier, cast the
	/// pointer to void* or a pointer to an integral or a floating point type when passing it to <c>PIXBeginEvent</c>. For the best
	/// performance, use a statically allocated string.
	/// </para>
	/// <para>
	/// Calls to <c>PIXBeginEvent</c> are guaranteed at least 512 bytes of space to save the record data, which includes the full size and
	/// alignment of the format string and all variables. In general, PIX events are intended for short high-performance markers that align
	/// to your game's major components, systems, or content.
	/// </para>
	/// <para>
	/// This method is used to time CPU events. For more information about timing GPU events, see the <c>PIX GPU Capture APIs</c> section of <c>PIX3</c>.
	/// </para>
	/// <para>
	/// Each call to <c>PIXBeginEvent</c> must have a matching call to <c>PIXEndEvent</c>. The paired calls to <c>PIXBeginEvent</c> and
	/// <c>PIXEndEvent</c> must occur on the same thread. The timing interval is about 200ns, and there is a low overhead to using this
	/// function, so up to several hundred thousand calls to <c>PIXBeginEvent</c> can be made per second.
	/// </para>
	/// <para><c>PIXBeginEvent</c> and <c>PIXEndEvent</c> pairs can be nested to any depth.</para>
	/// </remarks>
	// https://learn.microsoft.com/en-us/gaming/gdk/_content/gc/reference/tools/pix3/functions/pixbeginevent_4 void PIXBeginEvent( void*
	// context, UINT64 color, PCWSTR formatString, ... )
	[PInvokeData("pix_win.h")]
	public static void PIXBeginEvent(ID3D12GraphicsCommandList context, ulong color, string formatString, params object?[] args)
	{
		using SafeLPWSTR str = new(string.Format(formatString, args));
		context.BeginEvent(0, str, str.Size);
	}

	/// <summary>
	/// Starts a user-defined event for a timing capture of CPU activity, to be displayed in the <b>System Timing Capture</b> feature of PIX.
	/// </summary>
	/// <param name="context">
	/// Context for the event, accepts <c>ID3D12GraphicsCommandList\*</c>, <c>ID3D12GraphicsCommandList\*</c> and
	/// <c>ID3D12XboxDmaCommandList\*</c> (Xbox only).
	/// </param>
	/// <param name="color">
	/// The event color to use in the system timing chart. Use <c>PIX_COLOR</c> to specify a color, <c>PIX_COLOR_INDEX</c> to specify a
	/// color index, or pass in a raw DWORD noting that the format is ARGB and the alpha channel value must be 0xff.
	/// </param>
	/// <param name="formatString">
	/// The name to use to describe the event, as a pointer to a null-terminated Unicode string. The string may specify zero or more
	/// optional string format placeholders, very similar to <c>sprintf</c> formatting.
	/// </param>
	/// <param name="args">
	/// If placeholders are used in formatString, there must be a corresponding number of parameters whose types depend on the placeholders.
	/// This method supports up to a maximum of 16 format parameters.
	/// </param>
	/// <remarks>
	/// <para>
	/// The <c>PIXBeginEvent</c> function saves format string and format parameters instead of formatting the string at runtime. Formatting
	/// is then done when reading capture files in PIX. Use 16-byte aligned strings (preferable) or 8-byte aligned strings with
	/// <c>PIXBeginEvent</c> to get the best performance. To print a char* or wchar_t* as a pointer using %p format specifier, cast the
	/// pointer to void* or a pointer to an integral or a floating point type when passing it to <c>PIXBeginEvent</c>. For the best
	/// performance, use a statically allocated string.
	/// </para>
	/// <para>
	/// Calls to <c>PIXBeginEvent</c> are guaranteed at least 512 bytes of space to save the record data, which includes the full size and
	/// alignment of the format string and all variables. In general, PIX events are intended for short high-performance markers that align
	/// to your game's major components, systems, or content.
	/// </para>
	/// <para>
	/// This method is used to time CPU events. For more information about timing GPU events, see the <c>PIX GPU Capture APIs</c> section of <c>PIX3</c>.
	/// </para>
	/// <para>
	/// Each call to <c>PIXBeginEvent</c> must have a matching call to <c>PIXEndEvent</c>. The paired calls to <c>PIXBeginEvent</c> and
	/// <c>PIXEndEvent</c> must occur on the same thread. The timing interval is about 200ns, and there is a low overhead to using this
	/// function, so up to several hundred thousand calls to <c>PIXBeginEvent</c> can be made per second.
	/// </para>
	/// <para><c>PIXBeginEvent</c> and <c>PIXEndEvent</c> pairs can be nested to any depth.</para>
	/// </remarks>
	// https://learn.microsoft.com/en-us/gaming/gdk/_content/gc/reference/tools/pix3/functions/pixbeginevent_4 void PIXBeginEvent( void*
	// context, UINT64 color, PCWSTR formatString, ... )
	[PInvokeData("pix_win.h")]
	public static void PIXBeginEvent(ID3DUserDefinedAnnotation context, ulong color, string formatString, params object?[] args) => context.BeginEvent(string.Format(formatString, args));

	/// <summary>
	/// Starts a user-defined event for a timing capture of CPU activity, to be displayed in the <b>System Timing Capture</b> feature of PIX.
	/// </summary>
	/// <param name="context">
	/// Context for the event, accepts <c>ID3D12GraphicsCommandList\*</c>, <c>ID3D12GraphicsCommandList\*</c> and
	/// <c>ID3D12XboxDmaCommandList\*</c> (Xbox only).
	/// </param>
	/// <param name="color">
	/// The event color to use in the system timing chart. Use <c>PIX_COLOR</c> to specify a color, <c>PIX_COLOR_INDEX</c> to specify a
	/// color index, or pass in a raw DWORD noting that the format is ARGB and the alpha channel value must be 0xff.
	/// </param>
	/// <param name="formatString">
	/// The name to use to describe the event, as a pointer to a null-terminated Unicode string. The string may specify zero or more
	/// optional string format placeholders, very similar to <c>sprintf</c> formatting.
	/// </param>
	/// <param name="args">
	/// If placeholders are used in formatString, there must be a corresponding number of parameters whose types depend on the placeholders.
	/// This method supports up to a maximum of 16 format parameters.
	/// </param>
	/// <remarks>
	/// <para>
	/// The <c>PIXBeginEvent</c> function saves format string and format parameters instead of formatting the string at runtime. Formatting
	/// is then done when reading capture files in PIX. Use 16-byte aligned strings (preferable) or 8-byte aligned strings with
	/// <c>PIXBeginEvent</c> to get the best performance. To print a char* or wchar_t* as a pointer using %p format specifier, cast the
	/// pointer to void* or a pointer to an integral or a floating point type when passing it to <c>PIXBeginEvent</c>. For the best
	/// performance, use a statically allocated string.
	/// </para>
	/// <para>
	/// Calls to <c>PIXBeginEvent</c> are guaranteed at least 512 bytes of space to save the record data, which includes the full size and
	/// alignment of the format string and all variables. In general, PIX events are intended for short high-performance markers that align
	/// to your game's major components, systems, or content.
	/// </para>
	/// <para>
	/// This method is used to time CPU events. For more information about timing GPU events, see the <c>PIX GPU Capture APIs</c> section of <c>PIX3</c>.
	/// </para>
	/// <para>
	/// Each call to <c>PIXBeginEvent</c> must have a matching call to <c>PIXEndEvent</c>. The paired calls to <c>PIXBeginEvent</c> and
	/// <c>PIXEndEvent</c> must occur on the same thread. The timing interval is about 200ns, and there is a low overhead to using this
	/// function, so up to several hundred thousand calls to <c>PIXBeginEvent</c> can be made per second.
	/// </para>
	/// <para><c>PIXBeginEvent</c> and <c>PIXEndEvent</c> pairs can be nested to any depth.</para>
	/// </remarks>
	// https://learn.microsoft.com/en-us/gaming/gdk/_content/gc/reference/tools/pix3/functions/pixbeginevent_4 void PIXBeginEvent( void*
	// context, UINT64 color, PCWSTR formatString, ... )
	[PInvokeData("pix_win.h")]
	public static void PIXBeginEvent(ID3D11DeviceContext2 context, ulong color, string formatString, params object?[] args) => context.BeginEventInt(string.Format(formatString, args), 0);

	/// <summary>Defines the end of a user-defined event.</summary>
	/// <param name="context">
	/// Context for the event, accepts <c>ID3D12GraphicsCommandList\*</c>, <c>ID3D12GraphicsCommandList\*</c>, and
	/// <c>ID3D12XboxDmaCommandList\*</c> (Xbox only). The context associates the PIX marker with the D3D12 object it is set on.
	/// </param>
	/// <remarks>
	/// Each call to <c>PIXEndEvent</c> must be paired with a corresponding call to <c>PIXBeginEvent</c>. The paired calls to
	/// <c>PIXBeginEvent</c> and <c>PIXEndEvent</c> must occur on the same thread. In the event of mismatched markers, PIX will throw a
	/// warning in a timing capture. Mismatched markers will cause an unexpected or unusable rendering of the PIX timeline. In a worst case
	/// scenario, mismatched event calls may cause PIX to crash.
	/// </remarks>
	// https://learn.microsoft.com/en-us/gaming/gdk/_content/gc/reference/tools/pix3/functions/pixendevent_2 void PIXEndEvent( void* context )
	[PInvokeData("pix_win.h")]
	public static void PIXEndEvent(ID3D12CommandQueue context) => context.EndEvent();

	/// <summary>Defines the end of a user-defined event.</summary>
	/// <param name="context">
	/// Context for the event, accepts <c>ID3D12GraphicsCommandList\*</c>, <c>ID3D12GraphicsCommandList\*</c>, and
	/// <c>ID3D12XboxDmaCommandList\*</c> (Xbox only). The context associates the PIX marker with the D3D12 object it is set on.
	/// </param>
	/// <remarks>
	/// Each call to <c>PIXEndEvent</c> must be paired with a corresponding call to <c>PIXBeginEvent</c>. The paired calls to
	/// <c>PIXBeginEvent</c> and <c>PIXEndEvent</c> must occur on the same thread. In the event of mismatched markers, PIX will throw a
	/// warning in a timing capture. Mismatched markers will cause an unexpected or unusable rendering of the PIX timeline. In a worst case
	/// scenario, mismatched event calls may cause PIX to crash.
	/// </remarks>
	// https://learn.microsoft.com/en-us/gaming/gdk/_content/gc/reference/tools/pix3/functions/pixendevent_2 void PIXEndEvent( void* context )
	[PInvokeData("pix_win.h")]
	public static void PIXEndEvent(ID3DUserDefinedAnnotation context) => context.EndEvent();

	/// <summary>Defines the end of a user-defined event.</summary>
	/// <param name="context">
	/// Context for the event, accepts <c>ID3D12GraphicsCommandList\*</c>, <c>ID3D12GraphicsCommandList\*</c>, and
	/// <c>ID3D12XboxDmaCommandList\*</c> (Xbox only). The context associates the PIX marker with the D3D12 object it is set on.
	/// </param>
	/// <remarks>
	/// Each call to <c>PIXEndEvent</c> must be paired with a corresponding call to <c>PIXBeginEvent</c>. The paired calls to
	/// <c>PIXBeginEvent</c> and <c>PIXEndEvent</c> must occur on the same thread. In the event of mismatched markers, PIX will throw a
	/// warning in a timing capture. Mismatched markers will cause an unexpected or unusable rendering of the PIX timeline. In a worst case
	/// scenario, mismatched event calls may cause PIX to crash.
	/// </remarks>
	// https://learn.microsoft.com/en-us/gaming/gdk/_content/gc/reference/tools/pix3/functions/pixendevent_2 void PIXEndEvent( void* context )
	[PInvokeData("pix_win.h")]
	public static void PIXEndEvent(ID3D11DeviceContext2 context) => context.EndEvent();

	/// <summary>Defines the end of a user-defined event.</summary>
	/// <param name="context">
	/// Context for the event, accepts <c>ID3D12GraphicsCommandList\*</c>, <c>ID3D12GraphicsCommandList\*</c>, and
	/// <c>ID3D12XboxDmaCommandList\*</c> (Xbox only). The context associates the PIX marker with the D3D12 object it is set on.
	/// </param>
	/// <remarks>
	/// Each call to <c>PIXEndEvent</c> must be paired with a corresponding call to <c>PIXBeginEvent</c>. The paired calls to
	/// <c>PIXBeginEvent</c> and <c>PIXEndEvent</c> must occur on the same thread. In the event of mismatched markers, PIX will throw a
	/// warning in a timing capture. Mismatched markers will cause an unexpected or unusable rendering of the PIX timeline. In a worst case
	/// scenario, mismatched event calls may cause PIX to crash.
	/// </remarks>
	// https://learn.microsoft.com/en-us/gaming/gdk/_content/gc/reference/tools/pix3/functions/pixendevent_2 void PIXEndEvent( void* context )
	[PInvokeData("pix_win.h")]
	public static void PIXEndEvent(ID3D12GraphicsCommandList context) => context.EndEvent();

	/// <inheritdoc/>
	public void Dispose()
	{
		GC.SuppressFinalize(this);
		switch (iUnk)
		{
			case ID3D12CommandQueue pCommandQueue:
				PIXEndEvent(pCommandQueue);
				break;
			case ID3D12GraphicsCommandList pCommandList:
				PIXEndEvent(pCommandList);
				break;
			case ID3DUserDefinedAnnotation pAnnotation:
				PIXEndEvent(pAnnotation);
				break;
			case ID3D11DeviceContext2 pContext:
				PIXEndEvent(pContext);
				break;
		}
		//Marshal.ReleaseComObject(iUnk);
	}
}