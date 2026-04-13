namespace Sampler;

public enum ElementType
{
	Assembly = 1,
	Class,
	Delegate,
	Enum,
	Field,
	Interface,
	Method,
	Namespace,
	Property,
	Struct,
}

internal interface IAsyncRefresh
{
	System.Threading.Tasks.Task RefreshAsync(CancellationToken cancellationToken, IProgress<int>? progress);
}

internal interface IElementInfo
{
	public IEnumerable<IElementInfo> Children => [];
	public ElementType ElementType { get; }
	public string? ImageUrl => null;
	public string Name { get; }
}

internal class AssemblyInfo(System.Reflection.Assembly assembly) : IElementInfo, IAsyncRefresh
{
	private readonly System.Reflection.Assembly assembly = assembly;
	private readonly List<NamespaceInfo> namespaces = [];
	public IEnumerable<IElementInfo> Children => namespaces;
	public ElementType ElementType => ElementType.Assembly;
	public string Name => assembly.GetName().Name ?? throw new InvalidOperationException("Assembly name cannot be null.");

	public async System.Threading.Tasks.Task RefreshAsync(CancellationToken cancellationToken, IProgress<int>? progress) => await RefreshAsync(
		() => assembly.GetExportedTypes(),
		async (types, getChunk, progress) =>
		{
			// Fill namespaces list with the namespaces found in the assembly showing progress starting at getChuck and ending at 100.
			int totalTypes = types?.Length ?? 0;
			HashSet<string> seenNamespaces = [];
			for (int i = 0; i < totalTypes; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var type = types![i];
				var ns = type.Namespace ?? string.Empty;
				if (seenNamespaces.Add(ns))
					namespaces.Add(new NamespaceInfo(ns, assembly));
				progress?.Report(getChunk + ((i + 1) * (100 - getChunk) / totalTypes));
			}
		},
		progress: progress,
		cancellationToken: cancellationToken);

	internal static async System.Threading.Tasks.Task RefreshAsync<T>(Func<T>? blockingMethod, Action<T?, int, IProgress<int>?> asyncProcessor,
		int getChunk = 30, int fakeProgressDelay = 500, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
	{
		int percent = 0;
		progress?.Report(percent);
		T? result = default;
		if (blockingMethod is not null)
		{
			using (Timer timer = new(_ => { if (percent < getChunk) progress?.Report(percent += 5); }, null, 0, fakeProgressDelay))
				result = await System.Threading.Tasks.Task.Run(blockingMethod, cancellationToken);
			progress?.Report(getChunk);
		}
		else
			getChunk = 0;

		// Call asyncProcessor with the result of blockingMethod, if asyncProcessor is not null.
		await System.Threading.Tasks.Task.Run(() => asyncProcessor(result, getChunk, progress), cancellationToken);
		progress?.Report(100);
	}
}

internal class ClassInfo(Type type) : TypeInfo(type)
{
	public override ElementType ElementType => ElementType.Class;
	public override string? ImageUrl => "class.png";
}

internal class DelegateInfo(Type type) : TypeInfo(type)
{
	public override ElementType ElementType => ElementType.Delegate;
	public override string? ImageUrl => "delegate.png";
}

internal class EnumInfo(Type type) : TypeInfo(type)
{
	public override ElementType ElementType => ElementType.Enum;
	public override string? ImageUrl => "enum.png";
}

internal class FieldInfo(System.Reflection.FieldInfo field) : IElementInfo
{
	public ElementType ElementType => ElementType.Field;
	public System.Reflection.FieldInfo Field => @field;
	public string Name => @field.Name;
}

internal class InterfaceInfo(Type type) : TypeInfo(type)
{
	public override ElementType ElementType => ElementType.Interface;
	public override string? ImageUrl => "interface.png";
}

internal class MethodInfo(System.Reflection.MethodInfo method) : IElementInfo
{
	public ElementType ElementType => ElementType.Method;
	public System.Reflection.MethodInfo Method => method;
	public string Name => method.Name;
}

internal class NamespaceInfo(string name, System.Reflection.Assembly assembly) : IElementInfo
{
	//private readonly System.Reflection.Assembly assembly = assembly;
	private readonly Lazy<List<TypeInfo>> types = new(() => [.. assembly.GetExportedTypes().Where(t => t.Namespace == name).Select(TypeInfo.MakeType)]);

	public IEnumerable<IElementInfo> Children => types.Value;
	public ElementType ElementType => ElementType.Namespace;
	public string Name => name;
}

internal class StructInfo(Type type) : TypeInfo(type)
{
	public override ElementType ElementType => ElementType.Struct;
	public override string? ImageUrl => "struct.png";
}

internal abstract class TypeInfo(Type type) : IElementInfo
{
	private readonly Lazy<List<DelegateInfo>> delegates = new(() => [.. type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Where(t => typeof(Delegate).IsAssignableFrom(t.FieldType)).Select(t => new DelegateInfo(t.FieldType))]);
	private readonly Lazy<List<FieldInfo>> fields = new(() => [.. type.GetFields(System.Reflection.BindingFlags.Public).Select(f => new FieldInfo(f))]);
	private readonly Lazy<List<MethodInfo>> methods = new(() => [.. type.GetMethods(System.Reflection.BindingFlags.Public).Select(m => new MethodInfo(m))]);
	private readonly Lazy<List<PropertyInfo>> properties = new(() => [.. type.GetProperties(System.Reflection.BindingFlags.Public).Select(p => new PropertyInfo(p))]);
	private readonly Lazy<List<MethodInfo>> staticMethods = new(() => [.. type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Select(m => new MethodInfo(m))]);
	private readonly Lazy<List<TypeInfo>> types = new(() => [.. type.GetNestedTypes(System.Reflection.BindingFlags.Public).Select(MakeType)]);

	public virtual IEnumerable<IElementInfo> Children => types.Value;
	public abstract ElementType ElementType { get; }
	public abstract string? ImageUrl { get; }
	public virtual string Name => type.Name;
	public Type Type => type;
	protected virtual IEnumerable<IElementInfo> Delegates => delegates.Value;
	protected virtual IEnumerable<IElementInfo> Fields => fields.Value;
	protected virtual IEnumerable<IElementInfo> Methods => methods.Value;
	protected virtual IEnumerable<IElementInfo> Properties => properties.Value;
	protected virtual IEnumerable<IElementInfo> StaticMethods => staticMethods.Value;

	internal static TypeInfo MakeType(Type type)
	{
		if (type.IsInterface)
			return new InterfaceInfo(type);
		else if (typeof(Delegate).IsAssignableFrom(type))
			return new DelegateInfo(type);
		else if (type.IsClass)
			return new ClassInfo(type);
		else if (type.IsEnum)
			return new EnumInfo(type);
		else return type.IsValueType ? (TypeInfo)new StructInfo(type) : throw new NotSupportedException($"Type {type.FullName} is not supported.");
	}
}

internal class PropertyInfo(System.Reflection.PropertyInfo property) : IElementInfo
{
	public ElementType ElementType => ElementType.Property;
	public string Name => property.Name;
	public System.Reflection.PropertyInfo Property => property;
}