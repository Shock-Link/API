using System.Reflection;
using System.Text.Json.Serialization;
using NpgsqlTypes;
using OpenShock.Common.JsonSerialization;

// ReSharper disable InconsistentNaming

namespace OpenShock.Common.Models;

[JsonConverter(typeof(PermissionTypeConverter))]
public enum PermissionType
{
    [PgName("shockers.use")] Shockers_Use,

    [PgName("shockers.edit")] Shockers_Edit,

    [PgName("shockers.pause")] Shockers_Pause,

    [PgName("devices.edit")] Devices_Edit,
    
    [PgName("devices.auth")] Devices_Auth
}

public static class PermissionTypeExtensions
{
    public static bool IsAllowed(this PermissionType permissionType, IReadOnlyCollection<PermissionType> permissions) =>
        IsAllowedInternal(permissions, permissionType);

    public static bool IsAllowed(this IReadOnlyCollection<PermissionType> permissions, PermissionType permissionType) =>
        IsAllowedInternal(permissions, permissionType);
    
    public static bool IsAllowedAllowOnNull(this IReadOnlyCollection<PermissionType>? permissions,
        PermissionType permissionType) => permissions == null || IsAllowedInternal(permissions, permissionType);
    
    private static bool IsAllowedInternal(IReadOnlyCollection<PermissionType> permissions, PermissionType permissionType)
    {
        // ReSharper disable once PossibleMultipleEnumeration
        return permissions.Contains(permissionType) || permissions.Any(x =>
            PermissionTypeBindings.PermissionTypeToName[x].Parents.Contains(permissionType));
    }
}

public sealed class ParentPermissionAttribute : Attribute
{
    public PermissionType PermissionType { get; }

    public ParentPermissionAttribute(PermissionType permissionType)
    {
        PermissionType = permissionType;
    }
}

public static class PermissionTypeBindings
{
    public static readonly IReadOnlyDictionary<PermissionType, PermissionTypeRecord> PermissionTypeToName;

    public static readonly IReadOnlyDictionary<string, PermissionTypeRecord> NameToPermissionType;

    static PermissionTypeBindings()
    {
        var bindings = GetBindings();
        PermissionTypeToName = bindings.ToDictionary(x => x.PermissionType, x => x);
        NameToPermissionType = bindings.ToDictionary(x => x.Name, x => x);
    }

    private static PermissionTypeRecord[] GetBindings()
    {
        var permissionTypes = Enum.GetValues<PermissionType>();
        var permissionTypeFields = typeof(PermissionType).GetFields();

        var bindings = new PermissionTypeRecord[permissionTypes.Length];

        for (var i = 0; i < permissionTypes.Length; i++)
        {
            var permissionType = permissionTypes[i];

            var field = permissionTypeFields.First(x => x.Name == permissionType.ToString());
            var parents = field.GetCustomAttributes<ParentPermissionAttribute>().Select(x => x.PermissionType);
            var name = field.GetCustomAttribute<PgNameAttribute>()!.PgName;

            bindings[i] = new PermissionTypeRecord(permissionType, name, parents.ToArray());
        }

        return bindings;
    }
}

public record PermissionTypeRecord(PermissionType PermissionType, string Name, ICollection<PermissionType> Parents);