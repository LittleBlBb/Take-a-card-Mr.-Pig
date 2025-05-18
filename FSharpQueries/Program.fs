open System
open UserStats

[<EntryPoint>]
let main argv =
    SQLitePCL.Batteries_V2.Init()

    let userId = 1
    let stats = getUserWinsAndLosses userId
    printfn "Победы/Поражения:"
    for s in stats do
        printfn "  %s: %d" s.Winner s.Count

    let avg = getAvgMovesPerSession userId
    printfn "Среднее количество ходов: %f" avg
    0
