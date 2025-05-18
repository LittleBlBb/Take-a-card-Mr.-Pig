module DbConnection

open Microsoft.Data.Sqlite

let getConnection () =
    let conn = new SqliteConnection("Data Source=../Database/game.db")
    conn.Open()
    conn
