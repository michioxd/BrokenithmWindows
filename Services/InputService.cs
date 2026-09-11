using BrokenithmWindows.Core.Input;
using BrokenithmWindows.Core.Models;

namespace BrokenithmWindows.Services;

public sealed class InputService
{
    private readonly object _sync = new();

    private readonly List<TouchPoint> _contacts = new(16);
    private readonly HashSet<uint> _testContacts = new(),
        _serviceContacts = new();
    private InputOptions _options = new();
    private InputState _state = InputState.Empty;
    public InputState Snapshot
    {
        get
        {
            lock (_sync)
                return _state;
        }
    }
    public int ContactCount
    {
        get
        {
            lock (_sync)
                return _contacts.Count;
        }
    }

    public void Configure(InputOptions options)
    {
        lock (_sync)
        {
            _options = options;
            Clear();
        }
    }

    public void Press(TouchPoint point)
    {
        lock (_sync)
        {
            int index = Index(point.Id);
            if (index < 0)
                _contacts.Add(point);
            else
                _contacts[index] = point;
            Update();
        }
    }

    public void Move(TouchPoint point)
    {
        lock (_sync)
        {
            int index = Index(point.Id);
            if (index >= 0)
            {
                _contacts[index] = point;
                Update();
            }
        }
    }

    public void Release(uint id)
    {
        lock (_sync)
        {
            int index = Index(id);
            if (index >= 0)
                _contacts.RemoveAt(index);
            _testContacts.Remove(id);
            _serviceContacts.Remove(id);
            Update();
        }
    }

    public void SetButton(uint id, bool test)
    {
        lock (_sync)
        {
            (test ? _testContacts : _serviceContacts).Add(id);
            Update();
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _contacts.Clear();
            _testContacts.Clear();
            _serviceContacts.Clear();
            _state = InputState.Empty;
        }
    }

    private int Index(uint id)
    {
        for (int i = 0; i < _contacts.Count; i++)
            if (_contacts[i].Id == id)
                return i;
        return -1;
    }

    private void Update() =>
        _state = TouchProcessor.Process(_contacts, _options) with
        {
            Test = _testContacts.Count != 0,
            Service = _serviceContacts.Count != 0,
        };
}
