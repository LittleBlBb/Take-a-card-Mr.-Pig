module UserStats

open System
open Dapper
open DbConnection

type WinStat = {
    Winner: string
    Count: int
}

let getUserWinsAndLosses (userId: int) =
    use conn = getConnection ()
    conn.Query<WinStat>(
        "SELECT winner as Winner, COUNT(*) as Count FROM Sessions WHERE UserId = @userId GROUP BY winner",
        {| userId = userId |}
    )
    |> Seq.toList

let getAvgMovesPerSession (userId: int) =
    use conn = getConnection ()
    conn.ExecuteScalar<float>(
        """
        SELECT AVG(MoveCount) FROM (
            SELECT COUNT(*) as MoveCount
            FROM Sessions s
            JOIN Moves m ON s.Id = m.SessionId
            WHERE s.UserId = @userId
            GROUP BY s.Id
        ) sub
        """,
        {| userId = userId |}
    )
