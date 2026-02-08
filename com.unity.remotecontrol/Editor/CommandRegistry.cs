using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.RemoteControl.Editor.Commands;

namespace Unity.RemoteControl.Editor
{
    public class CommandRegistry
    {
        private readonly Dictionary<string, ICommand> _commands = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase);

        public void Register(ICommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            _commands[command.Name] = command;
        }

        public void Unregister(string name)
        {
            _commands.Remove(name);
        }

        public bool TryGetCommand(string name, out ICommand command)
        {
            return _commands.TryGetValue(name, out command);
        }

        public IEnumerable<string> GetCommandNames()
        {
            return _commands.Keys;
        }

        public async Task<Response> ExecuteAsync(Request request)
        {
            if (request == null)
                return Response.Error("", "Request is null");

            if (string.IsNullOrEmpty(request.command))
                return Response.Error(request.id, "Command name is required");

            if (!_commands.TryGetValue(request.command, out var command))
                return Response.Error(request.id, $"Unknown command: {request.command}");

            try
            {
                return await command.ExecuteAsync(request);
            }
            catch (Exception ex)
            {
                return Response.Error(request.id, $"Command error: {ex.Message}");
            }
        }

        public void RegisterDefaults()
        {
            Register(new PingCommand());
            Register(new ListPrefabsCommand());
            Register(new CreatePrefabCommand());
            Register(new DeletePrefabCommand());
            Register(new DuplicatePrefabCommand());
            Register(new GetPrefabCommand());
            Register(new GetComponentCommand());
            Register(new SetPropertyCommand());
            Register(new AddComponentCommand());
            Register(new RemoveComponentCommand());
            Register(new AddGameObjectCommand());
            Register(new RemoveGameObjectCommand());
        }
    }
}
