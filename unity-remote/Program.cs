using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Text.Json.Nodes;
using UnityRemote;

// Show help if no arguments provided
if (args.Length == 0)
{
    args = new[] { "--help" };
}

// Global options
var hostOption = new Option<string>("--host", () => "localhost", "Server host");
var portOption = new Option<int>("--port", () => 6000, "Server port");
var jsonOption = new Option<bool>("--json", () => false, "Output raw JSON");
var timeoutOption = new Option<int>("--timeout", () => 30000, "Timeout in milliseconds");

var rootCommand = new RootCommand(
@"Unity Remote Control CLI - Control Unity Editor prefabs via TCP

This tool connects to a Unity Editor running the Remote Control server
(Window > Remote Control) and allows inspection and modification of prefabs.

PREFAB MANAGEMENT:
  create-prefab <path>              Create new prefab (--primitive Cube/Sphere/Capsule/Cylinder/Plane/Quad)
  delete-prefab <path>              Delete a prefab
  duplicate-prefab <src> <dest>     Duplicate a prefab
  list-prefabs                      List all prefabs (--folder to filter, --limit/--offset for pagination)
  get-prefab <path>                 Get hierarchy overview (--properties for values, --gameobject to focus)

COMPONENT OPERATIONS:
  get-component <path> <type>       Get component details with property values
  add-component <path> <type>       Add component to prefab root (--gameobject for children)
  remove-component <path> <type>    Remove component
  set-property <path> <comp> <prop> <value>   Set a property value

GAMEOBJECT OPERATIONS:
  add-gameobject <path>             Add child GameObject (--name, --parent)
  remove-gameobject <path> <go>     Remove child GameObject

ASSET OPERATIONS (ScriptableObjects, Materials, PhysicsMaterials, etc.):
  create-asset <path> <type>        Create a new asset (e.g., PhysicsMaterial2D, Material)
  get-asset <path>                  Inspect asset properties
  set-asset-property <path> <prop> <value>   Set an asset property
  list-assets                       List assets (--folder, --type to filter)

HIERARCHY PATHS:
  Use --gameobject or --parent with slash-separated paths to target nested GameObjects.
  Paths are relative to the prefab root (don't include the root name).

  Example hierarchy:
    Player                 (root)
    +-- Body
    |   +-- LeftArm
    |   |   +-- Hand       path: ""Body/LeftArm/Hand""
    |   +-- RightArm
    |       +-- Hand       path: ""Body/RightArm/Hand""
    +-- Head
        +-- Eye            path: ""Head/Eye""

EXAMPLES:
  # Get a brief hierarchy overview (default: names + component types, no properties)
  unityrc get-prefab Assets/Prefabs/Enemy.prefab

  # Get hierarchy with properties on the root GameObject
  unityrc get-prefab Assets/Prefabs/Enemy.prefab --properties

  # Focus on a specific child and see its properties
  unityrc get-prefab Assets/Prefabs/Enemy.prefab --gameobject Body/LeftArm --properties

  # Limit hierarchy depth (0 = root only, 1 = root + children, etc.)
  unityrc get-prefab Assets/Prefabs/Enemy.prefab --depth 2

  # List prefabs with pagination
  unityrc list-prefabs --folder Assets/Prefabs --limit 50 --offset 0

  # Basic operations
  unityrc ping
  unityrc create-prefab Assets/Prefabs/Enemy.prefab --primitive Cube
  unityrc get-prefab Assets/Prefabs/Enemy.prefab --json

  # Add nested GameObjects
  unityrc add-gameobject Assets/Prefabs/Enemy.prefab --name Body
  unityrc add-gameobject Assets/Prefabs/Enemy.prefab --name LeftArm --parent Body
  unityrc add-gameobject Assets/Prefabs/Enemy.prefab --name Hand --parent Body/LeftArm

  # Add components to nested objects (use --gameobject to specify path)
  unityrc add-component Assets/Prefabs/Enemy.prefab Rigidbody
  unityrc add-component Assets/Prefabs/Enemy.prefab BoxCollider --gameobject Body/LeftArm/Hand
  unityrc add-component Assets/Prefabs/Enemy.prefab Light --gameobject Head

  # Set properties on components (root or nested)
  unityrc set-property Assets/Prefabs/Enemy.prefab Rigidbody m_Mass 10
  unityrc set-property Assets/Prefabs/Enemy.prefab Transform m_LocalPosition ""[0,5,0]""
  unityrc set-property Assets/Prefabs/Enemy.prefab Transform m_LocalScale ""[2,2,2]"" --gameobject Body
  unityrc set-property Assets/Prefabs/Enemy.prefab Light m_Intensity 2.5 --gameobject Head

  # Get component from nested object
  unityrc get-component Assets/Prefabs/Enemy.prefab BoxCollider --gameobject Body/LeftArm/Hand

  # Remove nested GameObject
  unityrc remove-gameobject Assets/Prefabs/Enemy.prefab Body/LeftArm/Hand

  # Set an asset reference on a component (e.g., assign a PhysicsMaterial2D to a Collider2D)
  unityrc set-property Assets/Prefabs/Floor.prefab BoxCollider2D m_Material Assets/Physics/Bouncy.physicsMaterial2D

  # Set an internal prefab reference (e.g., point a script at a child GameObject)
  unityrc set-property Assets/Prefabs/Enemy.prefab EnemyAI m_Target Body/Head
  unityrc set-property Assets/Prefabs/Enemy.prefab EnemyAI m_TargetCollider ""Body/Head:SphereCollider""

  # Clear an object reference (set to null/none)
  unityrc set-property Assets/Prefabs/Floor.prefab BoxCollider2D m_Material """"

  # Asset operations (ScriptableObjects, Materials, etc.)
  unityrc create-asset Assets/Physics/Bouncy.physicsMaterial2D PhysicsMaterial2D
  unityrc get-asset Assets/Physics/Bouncy.physicsMaterial2D
  unityrc set-asset-property Assets/Physics/Bouncy.physicsMaterial2D bounciness 0.8
  unityrc list-assets Assets/Physics --type PhysicsMaterial2D")
{
    hostOption,
    portOption,
    timeoutOption
};

// Ping command
var pingCommand = new Command("ping", "Check server connectivity");
pingCommand.AddOption(jsonOption);
pingCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);

    await ExecuteCommand(host, port, timeout, json, "ping");
});
rootCommand.AddCommand(pingCommand);

// List prefabs command
var listPrefabsCommand = new Command("list-prefabs", "List all prefabs");
var folderOption = new Option<string?>("--folder", "Folder to search in");
var listOffsetOption = new Option<int>("--offset", () => 0, "Number of results to skip");
var listLimitOption = new Option<int>("--limit", () => 100, "Maximum number of results to return");
listPrefabsCommand.AddOption(folderOption);
listPrefabsCommand.AddOption(listOffsetOption);
listPrefabsCommand.AddOption(listLimitOption);
listPrefabsCommand.AddOption(jsonOption);
listPrefabsCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var folder = ctx.ParseResult.GetValueForOption(folderOption);
    var offset = ctx.ParseResult.GetValueForOption(listOffsetOption);
    var limit = ctx.ParseResult.GetValueForOption(listLimitOption);

    var parameters = new Dictionary<string, object?>();
    if (!string.IsNullOrEmpty(folder))
        parameters["folder"] = folder;
    parameters["offset"] = offset;
    parameters["limit"] = limit;

    await ExecuteCommand(host, port, timeout, json, "list_prefabs", parameters);
});
rootCommand.AddCommand(listPrefabsCommand);

// Create prefab command
var createPrefabCommand = new Command("create-prefab", "Create a new prefab");
var createPathArg = new Argument<string>("path", "Prefab asset path (e.g., Assets/Prefabs/MyPrefab.prefab)");
var primitiveOption = new Option<string?>("--primitive", "Create from primitive: Cube, Sphere, Capsule, Cylinder, Plane, Quad");
var nameOption = new Option<string?>("--name", "GameObject name (defaults to filename)");
createPrefabCommand.AddArgument(createPathArg);
createPrefabCommand.AddOption(primitiveOption);
createPrefabCommand.AddOption(nameOption);
createPrefabCommand.AddOption(jsonOption);
createPrefabCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var path = ctx.ParseResult.GetValueForArgument(createPathArg);
    var primitive = ctx.ParseResult.GetValueForOption(primitiveOption);
    var name = ctx.ParseResult.GetValueForOption(nameOption);

    var parameters = new Dictionary<string, object?> { ["path"] = path };
    if (!string.IsNullOrEmpty(primitive))
        parameters["primitive"] = primitive;
    if (!string.IsNullOrEmpty(name))
        parameters["name"] = name;

    await ExecuteCommand(host, port, timeout, json, "create_prefab", parameters);
});
rootCommand.AddCommand(createPrefabCommand);

// Delete prefab command
var deletePrefabCommand = new Command("delete-prefab", "Delete a prefab");
var deletePathArg = new Argument<string>("path", "Prefab asset path to delete");
deletePrefabCommand.AddArgument(deletePathArg);
deletePrefabCommand.AddOption(jsonOption);
deletePrefabCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var path = ctx.ParseResult.GetValueForArgument(deletePathArg);

    await ExecuteCommand(host, port, timeout, json, "delete_prefab", new Dictionary<string, object?> { ["path"] = path });
});
rootCommand.AddCommand(deletePrefabCommand);

// Duplicate prefab command
var duplicatePrefabCommand = new Command("duplicate-prefab", "Duplicate a prefab");
var sourcePathArg = new Argument<string>("source-path", "Source prefab path");
var destPathArg = new Argument<string>("dest-path", "Destination prefab path");
duplicatePrefabCommand.AddArgument(sourcePathArg);
duplicatePrefabCommand.AddArgument(destPathArg);
duplicatePrefabCommand.AddOption(jsonOption);
duplicatePrefabCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var sourcePath = ctx.ParseResult.GetValueForArgument(sourcePathArg);
    var destPath = ctx.ParseResult.GetValueForArgument(destPathArg);

    var parameters = new Dictionary<string, object?>
    {
        ["source_path"] = sourcePath,
        ["dest_path"] = destPath
    };

    await ExecuteCommand(host, port, timeout, json, "duplicate_prefab", parameters);
});
rootCommand.AddCommand(duplicatePrefabCommand);

// Get prefab command
var getPrefabCommand = new Command("get-prefab", "Get prefab hierarchy and components");
var pathArgument = new Argument<string>("path", "Prefab asset path");
var propertiesOption = new Option<bool>("--properties", "Include property values on the focus node");
var depthOption = new Option<int>("--depth", () => -1, "Max hierarchy depth (-1 = unlimited, 0 = root only)");
var getPrefabGoOption = new Option<string?>("--gameobject", "Focus on a specific child GameObject path");
getPrefabCommand.AddArgument(pathArgument);
getPrefabCommand.AddOption(propertiesOption);
getPrefabCommand.AddOption(depthOption);
getPrefabCommand.AddOption(getPrefabGoOption);
getPrefabCommand.AddOption(jsonOption);
getPrefabCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var path = ctx.ParseResult.GetValueForArgument(pathArgument);
    var properties = ctx.ParseResult.GetValueForOption(propertiesOption);
    var depth = ctx.ParseResult.GetValueForOption(depthOption);
    var goPath = ctx.ParseResult.GetValueForOption(getPrefabGoOption);

    var parameters = new Dictionary<string, object?>
    {
        ["path"] = path,
        ["include_properties"] = properties,
        ["max_depth"] = depth
    };
    if (!string.IsNullOrEmpty(goPath))
        parameters["gameobject_path"] = goPath;

    await ExecuteCommand(host, port, timeout, json, "get_prefab", parameters);
});
rootCommand.AddCommand(getPrefabCommand);

// Get component command
var getComponentCommand = new Command("get-component", "Get component details");
var prefabPathArg = new Argument<string>("prefab-path", "Prefab asset path");
var componentTypeArg = new Argument<string>("component-type", "Component type name");
var gameObjectPathOption = new Option<string?>("--gameobject", "Path to child GameObject");
getComponentCommand.AddArgument(prefabPathArg);
getComponentCommand.AddArgument(componentTypeArg);
getComponentCommand.AddOption(gameObjectPathOption);
getComponentCommand.AddOption(jsonOption);
getComponentCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var prefabPath = ctx.ParseResult.GetValueForArgument(prefabPathArg);
    var componentType = ctx.ParseResult.GetValueForArgument(componentTypeArg);
    var goPath = ctx.ParseResult.GetValueForOption(gameObjectPathOption);

    var parameters = new Dictionary<string, object?>
    {
        ["path"] = prefabPath,
        ["component_type"] = componentType
    };
    if (!string.IsNullOrEmpty(goPath))
        parameters["gameobject_path"] = goPath;

    await ExecuteCommand(host, port, timeout, json, "get_component", parameters);
});
rootCommand.AddCommand(getComponentCommand);

// Set property command
var setPropertyCommand = new Command("set-property", "Set a component property");
var setPrefabPathArg = new Argument<string>("prefab-path", "Prefab asset path");
var setComponentTypeArg = new Argument<string>("component-type", "Component type name");
var propertyPathArg = new Argument<string>("property-path", "Property path");
var valueArg = new Argument<string>("value", "New value (JSON format for complex types)");
var setGameObjectPathOption = new Option<string?>("--gameobject", "Path to child GameObject");
setPropertyCommand.AddArgument(setPrefabPathArg);
setPropertyCommand.AddArgument(setComponentTypeArg);
setPropertyCommand.AddArgument(propertyPathArg);
setPropertyCommand.AddArgument(valueArg);
setPropertyCommand.AddOption(setGameObjectPathOption);
setPropertyCommand.AddOption(jsonOption);
setPropertyCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var prefabPath = ctx.ParseResult.GetValueForArgument(setPrefabPathArg);
    var componentType = ctx.ParseResult.GetValueForArgument(setComponentTypeArg);
    var propertyPath = ctx.ParseResult.GetValueForArgument(propertyPathArg);
    var value = ctx.ParseResult.GetValueForArgument(valueArg);
    var goPath = ctx.ParseResult.GetValueForOption(setGameObjectPathOption);

    object? parsedValue = value;

    // Try to parse as JSON for complex types
    if (value.StartsWith("[") || value.StartsWith("{"))
    {
        parsedValue = value; // Keep as string, server will parse
    }
    else if (bool.TryParse(value, out var boolVal))
    {
        parsedValue = boolVal;
    }
    else if (int.TryParse(value, out var intVal))
    {
        parsedValue = intVal;
    }
    else if (double.TryParse(value, out var doubleVal))
    {
        parsedValue = doubleVal;
    }

    var parameters = new Dictionary<string, object?>
    {
        ["path"] = prefabPath,
        ["component_type"] = componentType,
        ["property_path"] = propertyPath,
        ["value"] = parsedValue
    };
    if (!string.IsNullOrEmpty(goPath))
        parameters["gameobject_path"] = goPath;

    await ExecuteCommand(host, port, timeout, json, "set_property", parameters);
});
rootCommand.AddCommand(setPropertyCommand);

// Add component command
var addComponentCommand = new Command("add-component", "Add a component to a GameObject");
var addPrefabPathArg = new Argument<string>("prefab-path", "Prefab asset path");
var addComponentTypeArg = new Argument<string>("component-type", "Component type to add");
var addGameObjectPathOption = new Option<string?>("--gameobject", "Path to child GameObject");
addComponentCommand.AddArgument(addPrefabPathArg);
addComponentCommand.AddArgument(addComponentTypeArg);
addComponentCommand.AddOption(addGameObjectPathOption);
addComponentCommand.AddOption(jsonOption);
addComponentCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var prefabPath = ctx.ParseResult.GetValueForArgument(addPrefabPathArg);
    var componentType = ctx.ParseResult.GetValueForArgument(addComponentTypeArg);
    var goPath = ctx.ParseResult.GetValueForOption(addGameObjectPathOption);

    var parameters = new Dictionary<string, object?>
    {
        ["path"] = prefabPath,
        ["component_type"] = componentType
    };
    if (!string.IsNullOrEmpty(goPath))
        parameters["gameobject_path"] = goPath;

    await ExecuteCommand(host, port, timeout, json, "add_component", parameters);
});
rootCommand.AddCommand(addComponentCommand);

// Remove component command
var removeComponentCommand = new Command("remove-component", "Remove a component from a GameObject");
var removePrefabPathArg = new Argument<string>("prefab-path", "Prefab asset path");
var removeComponentTypeArg = new Argument<string>("component-type", "Component type to remove");
var removeGameObjectPathOption = new Option<string?>("--gameobject", "Path to child GameObject");
removeComponentCommand.AddArgument(removePrefabPathArg);
removeComponentCommand.AddArgument(removeComponentTypeArg);
removeComponentCommand.AddOption(removeGameObjectPathOption);
removeComponentCommand.AddOption(jsonOption);
removeComponentCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var prefabPath = ctx.ParseResult.GetValueForArgument(removePrefabPathArg);
    var componentType = ctx.ParseResult.GetValueForArgument(removeComponentTypeArg);
    var goPath = ctx.ParseResult.GetValueForOption(removeGameObjectPathOption);

    var parameters = new Dictionary<string, object?>
    {
        ["path"] = prefabPath,
        ["component_type"] = componentType
    };
    if (!string.IsNullOrEmpty(goPath))
        parameters["gameobject_path"] = goPath;

    await ExecuteCommand(host, port, timeout, json, "remove_component", parameters);
});
rootCommand.AddCommand(removeComponentCommand);

// Add GameObject command
var addGameObjectCommand = new Command("add-gameobject", "Add a child GameObject");
var addGoPrefabPathArg = new Argument<string>("prefab-path", "Prefab asset path");
var goNameOption = new Option<string>("--name", () => "New GameObject", "Name for the new GameObject");
var parentPathOption = new Option<string?>("--parent", "Parent path within prefab");
addGameObjectCommand.AddArgument(addGoPrefabPathArg);
addGameObjectCommand.AddOption(goNameOption);
addGameObjectCommand.AddOption(parentPathOption);
addGameObjectCommand.AddOption(jsonOption);
addGameObjectCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var prefabPath = ctx.ParseResult.GetValueForArgument(addGoPrefabPathArg);
    var name = ctx.ParseResult.GetValueForOption(goNameOption)!;
    var parentPath = ctx.ParseResult.GetValueForOption(parentPathOption);

    var parameters = new Dictionary<string, object?>
    {
        ["path"] = prefabPath,
        ["name"] = name
    };
    if (!string.IsNullOrEmpty(parentPath))
        parameters["parent_path"] = parentPath;

    await ExecuteCommand(host, port, timeout, json, "add_gameobject", parameters);
});
rootCommand.AddCommand(addGameObjectCommand);

// Remove GameObject command
var removeGameObjectCommand = new Command("remove-gameobject", "Remove a child GameObject");
var removeGoPrefabPathArg = new Argument<string>("prefab-path", "Prefab asset path");
var removeGoPathArg = new Argument<string>("gameobject-path", "Path to the GameObject to remove");
removeGameObjectCommand.AddArgument(removeGoPrefabPathArg);
removeGameObjectCommand.AddArgument(removeGoPathArg);
removeGameObjectCommand.AddOption(jsonOption);
removeGameObjectCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var prefabPath = ctx.ParseResult.GetValueForArgument(removeGoPrefabPathArg);
    var goPath = ctx.ParseResult.GetValueForArgument(removeGoPathArg);

    var parameters = new Dictionary<string, object?>
    {
        ["path"] = prefabPath,
        ["gameobject_path"] = goPath
    };

    await ExecuteCommand(host, port, timeout, json, "remove_gameobject", parameters);
});
rootCommand.AddCommand(removeGameObjectCommand);

// Create asset command
var createAssetCommand = new Command("create-asset", "Create a new asset (ScriptableObject, Material, etc.)");
var createAssetPathArg = new Argument<string>("path", "Asset path (e.g., Assets/Physics/Bouncy.physicsMaterial2D)");
var createAssetTypeArg = new Argument<string>("type", "Asset type name (e.g., PhysicsMaterial2D, Material)");
createAssetCommand.AddArgument(createAssetPathArg);
createAssetCommand.AddArgument(createAssetTypeArg);
createAssetCommand.AddOption(jsonOption);
createAssetCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var path = ctx.ParseResult.GetValueForArgument(createAssetPathArg);
    var type = ctx.ParseResult.GetValueForArgument(createAssetTypeArg);

    var parameters = new Dictionary<string, object?>
    {
        ["path"] = path,
        ["type"] = type
    };

    await ExecuteCommand(host, port, timeout, json, "create_asset", parameters);
});
rootCommand.AddCommand(createAssetCommand);

// Get asset command
var getAssetCommand = new Command("get-asset", "Inspect an asset's properties");
var getAssetPathArg = new Argument<string>("path", "Asset path");
getAssetCommand.AddArgument(getAssetPathArg);
getAssetCommand.AddOption(jsonOption);
getAssetCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var path = ctx.ParseResult.GetValueForArgument(getAssetPathArg);

    await ExecuteCommand(host, port, timeout, json, "get_asset", new Dictionary<string, object?> { ["path"] = path });
});
rootCommand.AddCommand(getAssetCommand);

// Set asset property command
var setAssetPropertyCommand = new Command("set-asset-property", "Set a property on an asset");
var setAssetPathArg = new Argument<string>("path", "Asset path");
var setAssetPropArg = new Argument<string>("property", "Property path");
var setAssetValueArg = new Argument<string>("value", "New value (JSON format for complex types)");
setAssetPropertyCommand.AddArgument(setAssetPathArg);
setAssetPropertyCommand.AddArgument(setAssetPropArg);
setAssetPropertyCommand.AddArgument(setAssetValueArg);
setAssetPropertyCommand.AddOption(jsonOption);
setAssetPropertyCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var path = ctx.ParseResult.GetValueForArgument(setAssetPathArg);
    var property = ctx.ParseResult.GetValueForArgument(setAssetPropArg);
    var value = ctx.ParseResult.GetValueForArgument(setAssetValueArg);

    object? parsedValue = value;
    if (value.StartsWith("[") || value.StartsWith("{"))
    {
        parsedValue = value;
    }
    else if (bool.TryParse(value, out var boolVal))
    {
        parsedValue = boolVal;
    }
    else if (int.TryParse(value, out var intVal))
    {
        parsedValue = intVal;
    }
    else if (double.TryParse(value, out var doubleVal))
    {
        parsedValue = doubleVal;
    }

    var parameters = new Dictionary<string, object?>
    {
        ["path"] = path,
        ["property_path"] = property,
        ["value"] = parsedValue
    };

    await ExecuteCommand(host, port, timeout, json, "set_asset_property", parameters);
});
rootCommand.AddCommand(setAssetPropertyCommand);

// List assets command
var listAssetsCommand = new Command("list-assets", "List assets in a folder");
var listAssetsFolderOption = new Option<string?>("--folder", "Folder to search in");
var listAssetsTypeOption = new Option<string?>("--type", "Filter by asset type (e.g., PhysicsMaterial2D, Material)");
var listAssetsOffsetOption = new Option<int>("--offset", () => 0, "Number of results to skip");
var listAssetsLimitOption = new Option<int>("--limit", () => 100, "Maximum number of results to return");
listAssetsCommand.AddOption(listAssetsFolderOption);
listAssetsCommand.AddOption(listAssetsTypeOption);
listAssetsCommand.AddOption(listAssetsOffsetOption);
listAssetsCommand.AddOption(listAssetsLimitOption);
listAssetsCommand.AddOption(jsonOption);
listAssetsCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var folder = ctx.ParseResult.GetValueForOption(listAssetsFolderOption);
    var type = ctx.ParseResult.GetValueForOption(listAssetsTypeOption);
    var offset = ctx.ParseResult.GetValueForOption(listAssetsOffsetOption);
    var limit = ctx.ParseResult.GetValueForOption(listAssetsLimitOption);

    var parameters = new Dictionary<string, object?>();
    if (!string.IsNullOrEmpty(folder))
        parameters["folder"] = folder;
    if (!string.IsNullOrEmpty(type))
        parameters["type"] = type;
    parameters["offset"] = offset;
    parameters["limit"] = limit;

    await ExecuteCommand(host, port, timeout, json, "list_assets", parameters);
});
rootCommand.AddCommand(listAssetsCommand);

return await rootCommand.InvokeAsync(args);

async Task ExecuteCommand(string host, int port, int timeout, bool jsonOutput, string command, Dictionary<string, object?>? parameters = null)
{
    using var client = new TcpClientWrapper(host, port, timeout);

    try
    {
        await client.ConnectAsync();
    }
    catch (Exception ex)
    {
        if (jsonOutput)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
        else
        {
            Console.Error.WriteLine($"Connection failed: {ex.Message}");
        }
        Environment.ExitCode = 1;
        return;
    }

    try
    {
        var response = await client.SendCommandAsync(command, parameters);

        if (jsonOutput)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine(response?.ToJsonString(options));
        }
        else
        {
            var success = response?["success"]?.GetValue<bool>() ?? false;
            if (success)
            {
                var data = response?["data"];
                if (data != null)
                {
                    PrintFormattedOutput(data, 0);
                }
                else
                {
                    Console.WriteLine("Success");
                }
            }
            else
            {
                var error = response?["error"]?.GetValue<string>() ?? "Unknown error";
                Console.Error.WriteLine($"Error: {error}");
                Environment.ExitCode = 1;
            }
        }
    }
    catch (Exception ex)
    {
        if (jsonOutput)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
        else
        {
            Console.Error.WriteLine($"Command failed: {ex.Message}");
        }
        Environment.ExitCode = 1;
    }
}

void PrintFormattedOutput(JsonNode? node, int indent)
{
    if (node is JsonObject obj)
    {
        // Detect GameObjectInfo shape and use compact formatter
        if (obj.ContainsKey("componentNames") || obj.ContainsKey("components"))
        {
            PrintGameObject(obj, indent);
            return;
        }

        // Detect PrefabListResult shape
        if (obj.ContainsKey("prefabs") && obj.ContainsKey("total"))
        {
            PrintPrefabList(obj);
            return;
        }

        // Detect AssetListResult shape
        if (obj.ContainsKey("assets") && obj.ContainsKey("total"))
        {
            PrintAssetList(obj);
            return;
        }

        // Detect AssetInfo shape (has "type" and "properties" but not "componentNames")
        if (obj.ContainsKey("type") && obj.ContainsKey("properties") && !obj.ContainsKey("componentNames"))
        {
            PrintAssetInfo(obj);
            return;
        }

        // Generic object fallback
        var prefix = new string(' ', indent * 2);
        foreach (var prop in obj)
        {
            if (prop.Value is JsonObject || prop.Value is JsonArray)
            {
                Console.WriteLine($"{prefix}{prop.Key}:");
                PrintFormattedOutput(prop.Value, indent + 1);
            }
            else
            {
                Console.WriteLine($"{prefix}{prop.Key}: {prop.Value}");
            }
        }
    }
    else if (node is JsonArray arr)
    {
        var prefix = new string(' ', indent * 2);
        for (int i = 0; i < arr.Count; i++)
        {
            var item = arr[i];
            if (item is JsonObject itemObj)
            {
                var name = itemObj["name"]?.GetValue<string>() ?? itemObj["type"]?.GetValue<string>() ?? $"[{i}]";
                Console.WriteLine($"{prefix}- {name}");
                PrintFormattedOutput(item, indent + 1);
            }
            else
            {
                Console.WriteLine($"{prefix}- {item}");
            }
        }
    }
    else
    {
        var prefix = new string(' ', indent * 2);
        Console.WriteLine($"{prefix}{node}");
    }
}

void PrintGameObject(JsonObject go, int indent)
{
    var prefix = new string(' ', indent * 2);
    var name = go["name"]?.GetValue<string>() ?? "?";
    var childCount = go["childCount"]?.GetValue<int>() ?? 0;

    // Component names as compact bracket list
    var compNames = go["componentNames"]?.AsArray();
    var compStr = "";
    if (compNames != null && compNames.Count > 0)
    {
        compStr = " [" + string.Join(", ", compNames.Select(c => c?.GetValue<string>() ?? "?")) + "]";
    }

    Console.WriteLine($"{prefix}{name}{compStr}");

    // Full components (focus node with --properties)
    var components = go["components"]?.AsArray();
    if (components != null)
    {
        foreach (var comp in components)
        {
            if (comp is not JsonObject compObj) continue;
            var compType = compObj["type"]?.GetValue<string>() ?? "?";
            var props = compObj["properties"]?.AsArray();
            if (props == null || props.Count == 0)
            {
                Console.WriteLine($"{prefix}  {compType}: (no properties)");
                continue;
            }
            Console.WriteLine($"{prefix}  {compType}:");
            foreach (var p in props)
            {
                if (p is not JsonObject propObj) continue;
                var propName = propObj["name"]?.GetValue<string>() ?? "?";
                var propValue = propObj["value"];
                var valStr = propValue is JsonArray valArr
                    ? "[" + string.Join(", ", valArr.Select(v => v?.ToString() ?? "null")) + "]"
                    : propValue?.ToString() ?? "null";
                Console.WriteLine($"{prefix}    {propName}: {valStr}");
            }
        }
    }

    // Children
    var children = go["children"]?.AsArray();
    if (children != null && children.Count > 0)
    {
        foreach (var child in children)
        {
            if (child is JsonObject childObj)
                PrintGameObject(childObj, indent + 1);
        }
    }
    else if (childCount > 0)
    {
        Console.WriteLine($"{prefix}  ({childCount} children)");
    }
}

void PrintPrefabList(JsonObject obj)
{
    var total = obj["total"]?.GetValue<int>() ?? 0;
    var offset = obj["offset"]?.GetValue<int>() ?? 0;
    var limit = obj["limit"]?.GetValue<int>() ?? 0;
    var prefabs = obj["prefabs"]?.AsArray();

    if (prefabs == null || prefabs.Count == 0)
    {
        Console.WriteLine("No prefabs found.");
        return;
    }

    Console.WriteLine($"Showing {offset + 1}-{offset + prefabs.Count} of {total} prefabs:");
    foreach (var p in prefabs)
    {
        if (p is JsonObject pObj)
        {
            var path = pObj["path"]?.GetValue<string>() ?? "?";
            Console.WriteLine($"  {path}");
        }
    }

    if (offset + prefabs.Count < total)
        Console.WriteLine($"  ... use --offset {offset + prefabs.Count} to see more");
}

void PrintAssetInfo(JsonObject obj)
{
    var name = obj["name"]?.GetValue<string>() ?? "?";
    var type = obj["type"]?.GetValue<string>() ?? "?";
    var path = obj["path"]?.GetValue<string>() ?? "";

    Console.WriteLine($"{name} ({type})");
    if (!string.IsNullOrEmpty(path))
        Console.WriteLine($"  path: {path}");

    var properties = obj["properties"]?.AsArray();
    if (properties != null && properties.Count > 0)
    {
        foreach (var p in properties)
        {
            if (p is not JsonObject propObj) continue;
            var propName = propObj["name"]?.GetValue<string>() ?? "?";
            var propValue = propObj["value"];
            var valStr = propValue is JsonArray valArr
                ? "[" + string.Join(", ", valArr.Select(v => v?.ToString() ?? "null")) + "]"
                : propValue?.ToString() ?? "null";
            Console.WriteLine($"  {propName}: {valStr}");
        }
    }
}

void PrintAssetList(JsonObject obj)
{
    var total = obj["total"]?.GetValue<int>() ?? 0;
    var offset = obj["offset"]?.GetValue<int>() ?? 0;
    var assets = obj["assets"]?.AsArray();

    if (assets == null || assets.Count == 0)
    {
        Console.WriteLine("No assets found.");
        return;
    }

    Console.WriteLine($"Showing {offset + 1}-{offset + assets.Count} of {total} assets:");
    foreach (var a in assets)
    {
        if (a is JsonObject aObj)
        {
            var path = aObj["path"]?.GetValue<string>() ?? "?";
            var type = aObj["type"]?.GetValue<string>() ?? "";
            Console.WriteLine($"  {path} ({type})");
        }
    }

    if (offset + assets.Count < total)
        Console.WriteLine($"  ... use --offset {offset + assets.Count} to see more");
}
