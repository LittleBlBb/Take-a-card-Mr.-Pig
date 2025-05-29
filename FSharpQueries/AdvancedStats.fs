module AdvancedStats

open System.Collections.Generic
open Dapper
open DbConnection

// ---------- типы результата ----------
type WinRate = {
    Difficulty : int
    Wins       : int64
    Total      : int64
    Percent    : decimal
}

type PopularRank = {
    Rank : string
    C    : int64
}

type DiffLen = {
    Difficulty : int
    AvgLen     : decimal
}

// ---------- a) win-rate игрока по каждой сложности ----------
let getWinRateByDifficulty username =
    use conn = connection()
    let param = dict [ "username", box username ]
    conn.Query<WinRate>(
        """
        SELECT s."Difficulty",
               COUNT(*) FILTER (WHERE s."Winner" = @username)        AS Wins,
               COUNT(*)                                             AS Total,
               100.0 * COUNT(*) FILTER (WHERE s."Winner" = @username)
                     / NULLIF(COUNT(*),0)                           AS Percent
        FROM "Sessions" s
        JOIN "Users"    u ON u."Id" = s."UserId"
        WHERE u."Username" = @username
        GROUP BY s."Difficulty"
        ORDER BY s."Difficulty";
        """, param)
    |> Seq.toList                         // возвращаем список WinRate

// ---------- b) самый частый ранг, который запрашивал игрок ----------
let getMostRequestedRank username : PopularRank option =
    use conn = connection()
    let param = dict [ "username", box username ]
    let r =
        conn.QueryFirstOrDefault<PopularRank>(
            """
            SELECT m."PlayerRequest" AS Rank,
                   COUNT(*)          AS C
            FROM "Users" u
            JOIN "Sessions" s ON s."UserId" = u."Id"
            JOIN "Moves"    m ON m."SessionId" = s."Id"
            WHERE u."Username" = @username
            GROUP BY m."PlayerRequest"
            ORDER BY C DESC
            LIMIT 1;
            """, param)
    if obj.ReferenceEquals(r, null) then None else Some r

// ---------- c) средняя длина сессии (ходов) на каждой сложности ----------
let getDifficultyVsLen () =
    use conn = connection()
    conn.Query<DiffLen>(
        """
        SELECT sub."Difficulty",
               AVG(sub.cnt) AS AvgLen
        FROM (
            SELECT s."Difficulty",
                   COUNT(m."Id") AS cnt
            FROM "Sessions" s
            JOIN "Moves"    m ON m."SessionId" = s."Id"
            GROUP BY s."Id", s."Difficulty"
        ) sub
        GROUP BY sub."Difficulty"
        ORDER BY sub."Difficulty";
        """)
    |> Seq.toList
