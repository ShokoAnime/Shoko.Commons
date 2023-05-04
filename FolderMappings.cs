﻿using System;
using System.Collections.Generic;
using System.IO;
using Shoko.Models.Client;

namespace Shoko.Commons
{
    public class FolderMappings
    {
        public static FolderMappings Instance { get; }=new FolderMappings();

        private Dictionary<int, string> _mappings = new Dictionary<int, string>();
        private Action<Dictionary<int, string>> _saveFunction;
        private Func<Dictionary<int, string>> _loadFunction;
        private bool _wasloaded;

        private void LoadCheck()
        {
            if (_wasloaded)
                return;
            if (_loadFunction != null)
            {
                _mappings = _loadFunction();
                _wasloaded = true;
            }
        }

        public void SetLoadAndSaveCallback(Func<Dictionary<int, string>> loadFunction, Action<Dictionary<int, string>> saveFunction)
        {
            _loadFunction = loadFunction; 
            _saveFunction = saveFunction;
        }

        public void MapFolder(int folderid, string localpath)
        {
            LoadCheck();
            _mappings[folderid] = localpath;
            _saveFunction?.Invoke(_mappings);
        }

        public void UnMapFolder(int folderid)
        {
            LoadCheck();
            if (_mappings.ContainsKey(folderid))
                _mappings.Remove(folderid);
            _saveFunction?.Invoke(_mappings);
        }

        public string GetMapping(int folderid)
        {
            if (_mappings.ContainsKey(folderid))
                return _mappings[folderid];
            return string.Empty;
        }

        public bool IsValid(CL_ImportFolder impfolder) => !string.IsNullOrEmpty(TranslateDirectory(impfolder, string.Empty));

        public string TranslateFile(CL_ImportFolder impfolder, string path)
        {
            if (impfolder == null) return string.Empty;
            string result=TranslateFile(impfolder.ImportFolderID, path);
            try
            {
                if (result == string.Empty && Directory.Exists(impfolder.ImportFolderLocation))
                {
                    string npath = CombineNoChecks(impfolder.ImportFolderLocation, path);
                    if (File.Exists(npath))
                        return npath;
                }
            }
            catch
            {

            }
            return result;
        }
        public string TranslateDirectory(CL_ImportFolder impfolder, string path)
        {
            if (impfolder == null) return string.Empty;
            string result=TranslateDirectory(impfolder.ImportFolderID, path);
            try
            {
                if (result == string.Empty && Directory.Exists(impfolder.ImportFolderLocation))
                {
                    string npath = CombineNoChecks(impfolder.ImportFolderLocation, path);
                    if (Directory.Exists(npath))
                        return npath;
                }
            }
            catch
            {
                
            }
            return result;
        }

        public static string CombineNoChecks(string path1, string path2)
        {
            if (path2.StartsWith(Path.DirectorySeparatorChar.ToString()))
                path2 = path2.Substring(1);
            if (path2.Length == 0)
                return path1;
            if (path1.Length == 0)
                return path2;
            char ch = path1[path1.Length - 1];
            if (ch != Path.DirectorySeparatorChar && ch != Path.AltDirectorySeparatorChar &&
                ch != Path.VolumeSeparatorChar)
                return path1 + Path.DirectorySeparatorChar + path2;
            return path1 + path2;
        }

        public string TranslateFile(int folderid, string path)
        {
            LoadCheck();
            if (path == null)
                return string.Empty;
            if (_mappings == null) return string.Empty;
            if (!_mappings.ContainsKey(folderid) || string.IsNullOrEmpty(_mappings[folderid]))
                return string.Empty;
            string start = CombineNoChecks(_mappings[folderid], path);
            try
            {
                if (File.Exists(start))
                    return start;
            }
            catch
            {
                //TODO: Security issue
            }
            return string.Empty;
        }

        public string TranslateDirectory(int folderid, string path)
        {
            LoadCheck();
            if (path == null)
                return string.Empty;
            if (_mappings == null) return string.Empty;
            if (!_mappings.ContainsKey(folderid) || string.IsNullOrEmpty(_mappings[folderid]))
                return string.Empty;
            string start = CombineNoChecks(_mappings[folderid], path);
            try
            {
                if (Directory.Exists(start))
                    return start;
            }
            catch
            {
                //TODO: Security issue
            }
            return string.Empty;
        }
    }
}
