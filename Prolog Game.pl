% Игра "Берите карту, мистер Свин"
% FR1.1: Терминальное приложение для игры с ботом
% FR1.2: Два уровня сложности (легкий: случайный выбор, сложный: выбор ранга с максимумом карт)
% Дополнительно: При неудачном запросе игрок/бот берет случайную карту из колоды
% Изменение: При завершении игры не закрывать Prolog, а вывести имя победителя
% Вывод в консоль: на английском языке
% Комментарии: на русском

:- dynamic hand/2, score/2, turn/1, difficulty/1, deck/1.

% Определение рангов и мастей
ranks([ace, two, three, four, five, six, seven, eight, nine, ten, jack, queen, king]).
suits([spades, hearts, clubs, diamonds]).

% Инициализация колоды
init_deck :-
    retractall(deck(_)),
    ranks(Ranks),
    suits(Suits),
    findall(card(Rank, Suit), (member(Rank, Ranks), member(Suit, Suits)), Deck),
    assert(deck(Deck)).

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
    retractall(difficulty(_)),
    init_deck,
    draw_cards(7, PlayerHand),
    draw_cards(7, BotHand),
    assert(hand(player, PlayerHand)),
    assert(hand(bot, BotHand)),
    assert(score(player, 0)),
    assert(score(bot, 0)),
    random(0, 2, Turn),
    (Turn = 0 -> assert(turn(player)) ; assert(turn(bot))),
    assert(difficulty(easy)).

% Отображение руки игрока
show_hand(Player) :-
    hand(Player, Hand),
    write('Your hand:'), nl,
    (Hand = [] -> write('Empty') ; maplist(write_card, Hand)),
    nl.

% Форматирование карты для вывода
write_card(card(Rank, Suit)) :-
    format('~w of ~w~n', [Rank, Suit]).

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
        format('~w completed a set (~w)! Point added. Score: ~w~n', [Player, Rank, NewScore]),
        (NewScore = 5 -> end_game(Player) ; true)
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
        format('~w gave ~w ~w card(s) of rank ~w~n', [From, To, NumCards, Rank]),
        check_set(To, Rank),
        true
    ; 
        false).

% Взятие карты из колоды при неудачном запросе
draw_from_deck(Player) :-
    draw_cards(1, Cards),
    (Cards = [card(Rank, Suit)] ->
        hand(Player, Hand),
        append(Hand, Cards, NewHand),
        retract(hand(Player, Hand)),
        assert(hand(Player, NewHand)),
        format('~w drew ~w of ~w~n', [Player, Rank, Suit]),
        check_set(Player, Rank)
    ; 
        format('Deck is empty! No card drawn.~n', [])).

% Запрос карты
request_card(Player, Opponent, Rank, Result) :-
    hand(Player, Hand),
    (Hand \= [] ->
        (member(card(Rank, _), Hand) ->
            (transfer_cards(Rank, Opponent, Player) ->
                Result = continue
            ; 
                format('~w has no cards of rank ~w.~n', [Opponent, Rank]),
                draw_from_deck(Player),
                Result = switch)
        ; 
            format('You do not have rank ~w in your hand! Try again.~n', [Rank]),
            Result = retry)
    ; 
        format('~w has no cards! Turn passes.~n', [Player]),
        Result = switch).

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
        format('Bot requests: ~w~n', [Rank]),
        request_card(Bot, Player, Rank, Result)
    ; 
        format('Bot has no cards! Turn passes.~n', []),
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
        format('Bot requests: ~w~n', [BestRank]),
        request_card(Bot, Player, BestRank, Result)
    ; 
        format('Bot has no cards! Turn passes.~n', []),
        Result = switch).

% Ход бота в зависимости от сложности
bot_move(Bot, Player, Result) :-
    difficulty(Difficulty),
    (Difficulty = easy -> bot_move_easy(Bot, Player, Result) ; bot_move_hard(Bot, Player, Result)).

% Завершение игры
end_game(Winner) :-
    score(Winner, 5),
    format('Game over! ~w wins!~n', [Winner]).

% Основной игровой цикл
game_loop :-
    turn(Player),
    score(player, PlayerScore),
    score(bot, BotScore),
    format('Score: Player - ~w, Bot - ~w~n', [PlayerScore, BotScore]),
    (Player = player ->
        show_hand(player),
        player_move(Result),
        (Result = continue -> game_loop ;
         Result = switch -> switch_turn(player, bot), game_loop ;
         game_loop) % retry
    ; 
        bot_move(bot, player, Result),
        (Result = continue -> game_loop ;
         Result = switch -> switch_turn(bot, player), game_loop)).

% Ход игрока
player_move(Result) :-
    write('Enter rank (ace, two, ..., king): '),
    read(Rank),
    ranks(Ranks),
    (member(Rank, Ranks) ->
        request_card(player, bot, Rank, Result)
    ; 
        write('Invalid rank! Try again.'), nl,
        Result = retry).

% Установка уровня сложности
set_difficulty(Level) :-
    member(Level, [easy, hard]),
    retractall(difficulty(_)),
    assert(difficulty(Level)),
    format('Difficulty set to: ~w~n', [Level]).

% Запуск игры
play :-
    write('Welcome to "Take a Card, Mr. Pig"!'), nl,
    write('Choose difficulty (easy/hard): '),
    read(Difficulty),
    (set_difficulty(Difficulty) ->
        init_game,
        turn(First),
        format('First turn: ~w~n', [First]),
        game_loop
    ; 
        write('Invalid difficulty! Try again (easy/hard).'), nl,
        play).