using System;

namespace Ck.Database;

using System.IO;

public class IdFarm
{
    private readonly string _filePath;
    private int _currentId;

    public IdFarm(string databasePath)
    {
        _filePath = Path.Combine(databasePath, "IdFarm.json");

        if (File.Exists(_filePath))
        {
            // Load the last used ID from the file
            var content = File.ReadAllText(_filePath);
            _currentId = Convert.ToInt32(content.Trim());
        }
        else
        {
            // Initialize with 0 if the file does not exist
            _currentId = 0;
            Save();
        }
    }

    public int GetNextId()
    {
        _currentId++;
        Save();
        return _currentId;
    }

    private void Save()
    {
        // Save the current ID to the file
        File.WriteAllText(_filePath, _currentId.ToString());
    }
}
