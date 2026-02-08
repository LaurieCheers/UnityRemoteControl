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
var timeoutOption = new Option<int>("--timeout", () => 5000, "Timeout in milliseconds");

var rootCommand = new RootCommand(
@"Unity Remote Control CLI - Control Unity Editor prefabs via TCP

This tool connects to a Unity Editor running the Remote Control server
(Window > Remote Control) and allows inspection and modification of prefabs.

PREFAB MANAGEMENT:
  create-prefab <path>              Create new prefab (--primitive Cube/Sphere/Capsule/Cylinder/Plane/Quad)
  delete-prefab <path>              Delete a prefab
  duplicate-prefab <src> <dest>     Duplicate a prefab
  list-prefabs                      List all prefabs (--folder to filter)
  get-prefab <path>                 Get full hierarchy with components and values

COMPONENT OPERATIONS:
  get-component <path> <type>       Get component details with property values
  add-component <path> <type>       Add component to prefab root (--gameobject for children)
  remove-component <path> <type>    Remove component
  set-property <path> <comp> <prop> <value>   Set a property value

GAMEOBJECT OPERATIONS:
  add-gameobject <path>             Add child GameObject (--name, --parent)
  remove-gameobject <path> <go>     Remove child GameObject

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
  # Basic operations
  unityrc ping
  unityrc create-prefab Assets/Prefabs/Enemy.prefab --primitive Cube
  unityrc get-prefab Assets/Prefabs/Enemy.prefab --json
  unityrc list-prefabs --folder Assets/Prefabs

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
  unityrc remove-gameobject Assets/Prefabs/Enemy.prefab Body/LeftArm/Hand")
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
listPrefabsCommand.AddOption(folderOption);
listPrefabsCommand.AddOption(jsonOption);
listPrefabsCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var folder = ctx.ParseResult.GetValueForOption(folderOption);

    var parameters = new Dictionary<string, object?>();
    if (!string.IsNullOrEmpty(folder))
        parameters["folder"] = folder;

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
var noPropertiesOption = new Option<bool>("--no-properties", "Exclude property details");
getPrefabCommand.AddArgument(pathArgument);
getPrefabCommand.AddOption(noPropertiesOption);
getPrefabCommand.AddOption(jsonOption);
getPrefabCommand.SetHandler(async ctx =>
{
    var host = ctx.ParseResult.GetValueForOption(hostOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var json = ctx.ParseResult.GetValueForOption(jsonOption);
    var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
    var path = ctx.ParseResult.GetValueForArgument(pathArgument);
    var noProperties = ctx.ParseResult.GetValueForOption(noPropertiesOption);

    var parameters = new Dictionary<string, object?>
    {
        ["path"] = path,
        ["include_properties"] = !noProperties
    };

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
    var prefix = new string(' ', indent * 2);

    if (node is JsonObject obj)
    {
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
        Console.WriteLine($"{prefix}{node}");
    }
}
