% Игра "Берите карту, мистер Свин"
% FR1.1: Терминальное приложение для игры с ботом
% FR1.2: Два уровня сложности (легкий: случайный выбор, сложный: выбор ранга с максимумом карт)
% Дополнительно: При неудачном запросе игрок/бот берет случайную карту из колоды
% Изменение: При завершении игры не закрывать Prolog, а вывести имя победителя
% Вывод в консоль: на английском языке
% Комментарии: на русском

:- use_module(library(http/thread_httpd)).
:- use_module(library(http/http_dispatch)).
:- use_module(library(http/http_json)).

% Динамические предикаты для хранения состояния игры
:- dynamic hand/2, score/2, turn/1, difficulty/1, deck/1, memory/2.

% Определение рангов и мастей
ranks([ace, two, three, four, five, six, seven, eight, nine, ten, jack, queen, king]).
suits([spades, hearts, clubs, diamonds]).

% Инициализация колоды
init_deck :-
    retractall(deck(_)),
    ranks(Ranks),
    suits(Suits),
    findall(card(Rank, Suit), (member(Rank, Ranks), member(Suit, Suits)), Deck),
    random_permutation(Deck, ShuffledDeck),
    assert(deck(ShuffledDeck)).

% Получение N случайных карт из колоды
draw_cards(N, Cards) :-
    deck(Deck),
    length(Deck, Len),
    (Len >= N ->
        random_permutation(Deck, Shuffled),
        length(Cards, N),
        append(Cards, Remaining, Shuffled),
        retract(deck(Deck)),
        assert(deck(Remaining))
    ; Cards = []).

% Инициализация игры
init_game :-
    retractall(hand(_, _)),
    retractall(score(_, _)),
    retractall(turn(_)),
    retractall(memory(_, _)),
    init_deck,
    draw_cards(7, PlayerHand),
    draw_cards(7, BotHand),
    assert(hand(player, PlayerHand)),
    assert(hand(bot, BotHand)),
    assert(score(player, 0)),
    assert(score(bot, 0)),
    random(0, 2, Turn),
    (Turn = 0 -> assert(turn(player)) ; assert(turn(bot))).

% Проверка набора из 4 карт одного ранга
check_set(Player, Rank) :-
    hand(Player, Hand),
    findall(card(Rank, Suit), member(card(Rank, Suit), Hand), Cards),
    length(Cards, Count),
    (Count = 4 ->
        score(Player, OldScore),
        NewScore is OldScore + 1,
        retract(score(Player, OldScore)),
        assert(score(Player, NewScore)),
        subtract(Hand, Cards, NewHand),
        retract(hand(Player, Hand)),
        assert(hand(Player, NewHand)),
        (NewScore = 5 -> assert(winner(Player)) ; true)
    ; true).

% Передача карт от одного игрока другому
transfer_cards(Rank, From, To) :-
    hand(From, FromHand),
    findall(card(Rank, Suit), member(card(Rank, Suit), FromHand), Cards),
    length(Cards, NumCards),
    (NumCards > 0 ->
        subtract(FromHand, Cards, NewFromHand),
        retract(hand(From, FromHand)),
        assert(hand(From, NewFromHand)),
        hand(To, ToHand),
        append(ToHand, Cards, NewToHand),
        retract(hand(To, ToHand)),
        assert(hand(To, NewToHand)),
        check_set(To, Rank),
        true
    ; false).

% Взятие карты из колоды при неудачном запросе
draw_from_deck(Player) :-
    draw_cards(1, Cards),
    (Cards = [Card] ->
        hand(Player, Hand),
        append(Hand, Cards, NewHand),
        retract(hand(Player, Hand)),
        assert(hand(Player, NewHand)),
        Card = card(Rank, _),
        check_set(Player, Rank)
    ; true).

% Запоминание хода (FR1.6)
store_memory(Player, Rank) :-
    assert(memory(Player, Rank)).

% Запрос карты
request_card(Player, Opponent, Rank, EnableMemory, Result) :-
    hand(Player, Hand),
    (Hand \= [] ->
        ranks(Ranks),
        (member(Rank, Ranks) ->
            (member(card(Rank, _), Hand) ->
                (EnableMemory -> store_memory(Player, Rank) ; true),
                (transfer_cards(Rank, Opponent, Player) ->
                    Result = continue
                ; 
                    draw_from_deck(Player),
                    Result = switch)
            ; 
                Result = retry)
        ; 
            Result = invalid_rank)
    ; 
        Result = no_cards).

% Смена хода
switch_turn(Player, Opponent) :-
    retract(turn(Player)),
    assert(turn(Opponent)).

% Ход бота (легкий уровень: случайный выбор)
bot_move_easy(Bot, Player, Result) :-
    hand(Bot, Hand),
    (Hand \= [] ->
        findall(Rank, member(card(Rank, _), Hand), Ranks),
        random_member(Rank, Ranks),
        request_card(Bot, Player, Rank, false, Result)
    ; 
        Result = switch).

% Ход бота (сложный уровень: выбор ранга с максимумом карт)
bot_move_hard(Bot, Player, Result) :-
    hand(Bot, Hand),
    (Hand \= [] ->
        findall(Rank-Count, 
                (aggregate_all(count, member(card(Rank, _), Hand), Count), Count > 0), 
                RankCounts),
        sort(2, @>=, RankCounts, Sorted),
        Sorted = [BestRank-_|_],
        request_card(Bot, Player, BestRank, false, Result)
    ; 
        Result = switch).

% Ход бота в зависимости от сложности
bot_move(Bot, Player, Result) :-
    difficulty(Difficulty),
    (Difficulty = easy -> bot_move_easy(Bot, Player, Result) ; bot_move_hard(Bot, Player, Result)).

% HTTP: Запуск сервера
start_server(Port) :-
    http_server(http_dispatch, [port(Port)]).

% HTTP: Маршруты
:- http_handler('/set_difficulty', handle_set_difficulty, []).
:- http_handler('/move', handle_move, []).
:- http_handler('/status', handle_status, []).

% HTTP: Установка сложности
handle_set_difficulty(Request) :-
    http_read_json_dict(Request, Data),
    Difficulty = Data.difficulty,
    member(Difficulty, [easy, medium, hard, expert]),
    retractall(difficulty(_)),
    assert(difficulty(Difficulty)),
    init_game,
    reply_json_dict(_{result: "success", difficulty: Difficulty}).

% HTTP: Обработка хода
handle_move(Request) :-
    http_read_json_dict(Request, Data),
    Rank = Data.rank,
    EnableMemory = Data.enable_memory,
    turn(player),
    request_card(player, bot, Rank, EnableMemory, Result),
    (Result = switch -> switch_turn(player, bot) ; true),
    hand(player, PlayerHand),
    hand(bot, BotHand),
    score(player, PlayerScore),
    score(bot, BotScore),
    (winner(Winner) -> WinnerStatus = Winner ; WinnerStatus = null),
    reply_json_dict(_{result: Result, player_hand: PlayerHand, bot_hand_length: length(BotHand), 
                     player_score: PlayerScore, bot_score: BotScore, winner: WinnerStatus}).

% HTTP: Получение статуса игры
handle_status(_Request) :-
    hand(player, PlayerHand),
    hand(bot, BotHand),
    score(player, PlayerScore),
    score(bot, BotScore),
    turn(Turn),
    (winner(Winner) -> WinnerStatus = Winner ; WinnerStatus = null),
    reply_json_dict(_{player_hand: PlayerHand, bot_hand_length: length(BotHand), 
                     player_score: PlayerScore, bot_score: BotScore, turn: Turn, winner: WinnerStatus}).

% Запуск сервера на порту 8080
:- initialization(start_server(8080)).