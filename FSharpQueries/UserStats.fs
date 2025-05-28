module UserStats
open Dapper
open DbConnection

type WinLoss = {
    Username : string
    Wins     : int64
    Losses   : int64
}

type AvgMoves = {
    Username : string
    AvgMoves : decimal       
}

/// 1) Победы / поражения
let getUserWinsAndLosses () =
    use conn = connection()
    conn.Query<WinLoss>(
        """
        SELECT u."Username",
               COUNT(CASE WHEN s."Winner" = u."Username" THEN 1 END)  AS Wins,
               COUNT(CASE WHEN s."Winner" <> u."Username"
                          AND s."Winner" IS NOT NULL THEN 1 END)     AS Losses
        FROM "Users" u
        LEFT JOIN "Sessions" s ON s."UserId" = u."Id"
        GROUP BY u."Username"
        ORDER BY u."Username";
        """
    )
    |> Seq.toList

/// 2) Среднее число ходов на сессию
let getAvgMovesPerSession () =
    use conn = connection()
    conn.Query<AvgMoves>(
        """
        SELECT u."Username",
               COALESCE(AVG(sub.cnt),0) AS AvgMoves     -- numeric → decimal
        FROM "Users" u
        LEFT JOIN (
            SELECT s."UserId", COUNT(m."Id") AS cnt
            FROM "Sessions" s
            JOIN "Moves" m ON m."SessionId" = s."Id"
            GROUP BY s."UserId", s."Id"
        ) sub ON sub."UserId" = u."Id"
        GROUP BY u."Username"
        ORDER BY u."Username";
        """
    )
    |> Seq.toList
