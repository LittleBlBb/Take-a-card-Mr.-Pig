open UserStats
open AdvancedStats

[<EntryPoint>]
let main _ =
    // FR 3.1
    printfn "=== Wins / Losses ==="
    getUserWinsAndLosses ()
    |> List.iter (fun x -> printfn "%-10s %d wins / %d losses" x.Username x.Wins x.Losses)

    printfn "\n=== Avg moves per session ==="
    getAvgMovesPerSession ()
    |> List.iter (fun x -> printfn "%-10s %.2f moves" x.Username x.AvgMoves)

    // FR 3.3
    printfn "\n=== Win-rate by difficulty (testuser) ==="
    getWinRateByDifficulty "testuser"
    |> List.iter (fun x ->
        printfn "D:%d  %d/%d  (%.1f%%)" x.Difficulty x.Wins x.Total x.Percent)

    

    printfn "\nMost popular rank (testuser):"
    match getMostRequestedRank "testuser" with
    | None   -> printfn "нет данных"
    | Some r -> printfn "%s  (%d раз)" r.Rank r.C


    0
