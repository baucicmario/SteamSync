using System;
using System.Text.Json;
using Microsoft.Data.Sqlite;

class Program {
    static void Main() {
        try {
            using var connection = new SqliteConnection("Data Source=AsyncCache.db");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM AsyncCache WHERE scope='collections' AND key='';";
            using var reader = command.ExecuteReader();
            if (reader.Read()) {
                var json = reader.GetString(0);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("allMsixvcGames", out var msixvc)) {
                    var data = msixvc.GetProperty("data");
                    Console.WriteLine("allMsixvcGames count: " + data.GetProperty("totalCount").GetInt32());
                }
            }
        } catch (Exception ex) {
            Console.WriteLine(ex.Message);
        }
    }
}
