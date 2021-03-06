﻿using LunarLabs.Parser;
using LunarLabs.Parser.XML;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LunarLabs.Bots
{
    public class Storage
    {
        private string _path;
        private Dictionary<string, Collection> _storage = new Dictionary<string, Collection>();

        public Storage(string path)
        {
            path = path.Replace(@"\", "/");

            if (!path.EndsWith("/"))
            {
                path = path + "/";
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            this._path = path;
        }

        public void Synchronize()
        {
            foreach (var entry in _storage)
            {
                var storage = entry.Value;
                if (storage.Modified)
                {
                    Console.WriteLine($"Saving storage for {storage.Name}...");
                    storage.Save();
                }
            }
        }

        public Collection FindCollection(string name, bool canCreate = true)
        {
            name = name.ToLower();

            if (_storage.ContainsKey(name))
            {
                return _storage[name];
            }

            var result = new Collection(_path, name);
            var loaded = result.Load();

            if (!loaded && !canCreate)
            {
                return null;
            }

            _storage[name] = result;
            return result;
        }
    }

    public class Collection
    {
        public readonly string Name;
        public readonly string FileName;

        public bool Modified { get; private set; }

        private Dictionary<string, List<string>> _keystore = new Dictionary<string, List<string>>();

        public Collection(string path, string name)
        {
            this.Name = name;
            this.FileName = path + name + ".xml";
        }

        public void Visit(Action<string, List<string>> visitor)
        {
            lock (_keystore)
            {
                foreach (var entry in _keystore)
                {
                    visitor(entry.Key, entry.Value);
                }
            }
        }

        internal bool Load()
        {
            if (File.Exists(FileName))
            {
                var content = File.ReadAllText(FileName);
                var root = XMLReader.ReadFromString(content);
                root = root["entries"];
                foreach (var child in root.Children)
                {
                    var id = child.GetString("key");
                    var list = new List<string>();
                    foreach (var item in child.Children)
                    {
                        if (item.Name == "item")
                        {
                            list.Add(item.Value);
                        }
                    }

                    _keystore[id] = list;
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        internal bool Save()
        {
            try
            {
                var root = DataNode.CreateArray("entries");
                foreach (var entry in _keystore)
                {
                    var node = DataNode.CreateObject("entry");
                    node.AddField("key", entry.Key);

                    foreach (var temp in entry.Value)
                    {
                        var item = DataNode.CreateObject("item");
                        item.Value = temp;
                        node.AddNode(item);
                    }

                    root.AddNode(node);
                }

                var content = XMLWriter.WriteToString(root);
                File.WriteAllText(FileName, content);
                Modified = false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Append(MessageSender sender, string obj) 
        {
            Add(sender, obj, true);
        }

        public void Set(MessageSender sender, string obj) 
        {
            if (obj == null)
            {
                obj = "";
            }

            var old = Get(sender);
            if (old != null && old == obj)
            {
                return;
            }

            Add(sender, obj, false);
        }

        private void Add(MessageSender sender, string obj, bool append)
        {
            lock (_keystore)
            {
                var tag = sender.Tag;
                List<string> list;

                if (_keystore.ContainsKey(tag))
                {
                    list = _keystore[tag];
                }
                else
                {
                    list = new List<string>();
                    _keystore[tag] = list;
                }

                if (list.Count > 0 && !append)
                {
                    list.Clear();
                }

                list.Add(obj);

                Modified = true;
            }
        }

        public void Remove(MessageSender sender)
        {
            var tag = sender.Tag;

            lock (_keystore)
            {
                if (_keystore.ContainsKey(tag))
                {
                    _keystore.Remove(tag);
                    Modified = true;
                }
            }
        }

        public bool Remove(MessageSender sender, string entry, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            lock (_keystore)
            {
                var tag = sender.Tag;
                if (_keystore.ContainsKey(tag))
                {
                    var list = _keystore[tag];
                    var oldCount = list.Count;
                    if (oldCount > 0)
                    {
                        list = list.Where(x => !x.Equals(entry, comparison)).ToList();

                        var newCount = list.Count;
                        if (newCount == oldCount)
                        {
                            return false;
                        }

                        if (newCount == 0)
                        {
                            _keystore.Remove(tag);
                        }
                        else
                        {
                            _keystore[tag] = list;
                        }

                        Modified = true;
                        return true;
                    }
                }

                return false;
            }
        }

        public string Get(MessageSender sender)
        {
            lock (_keystore)
            {
                var tag = sender.Tag;
                if (_keystore.ContainsKey(tag))
                {
                    return _keystore[tag].FirstOrDefault();
                }
                else
                {
                    return null;
                }
            }
        }

        public IEnumerable<string> List(MessageSender sender)
        {
            var tag = sender.Tag;
            lock (_keystore)
            {
                if (_keystore.ContainsKey(tag))
                {
                    return _keystore[tag];
                }
                else
                {
                    return Array.Empty<string>();
                }
            }
        }

        public bool Contains(MessageSender sender)
        {
            lock (_keystore)
            {
                var tag = sender.Tag;
                return _keystore.ContainsKey(tag);
            }
        }

        public int Count(MessageSender sender)
        {
            lock (_keystore)
            {
                var tag = sender.Tag;
                return _keystore.ContainsKey(tag) ? _keystore[tag].Count : 0;
            }
        }
    }
}
