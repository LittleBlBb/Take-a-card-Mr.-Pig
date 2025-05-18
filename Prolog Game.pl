:- dynamic hand/2, score/2, turn/1, difficulty/1, deck/1, game_state/1, http_mode/0, requested_rank/2.

% HTTP server dependencies
:- use_module(library(http/thread_httpd)).
:- use_module(library(http/http_dispatch)).
:- use_module(library(http/http_json)).

% Card definitions: rank and suit
ranks([ace, two, three, four, five, six, seven, eight, nine, ten, jack, queen, king]).
suits([spades, hearts, clubs, diamonds]).

% Convert card to JSON-serializable dictionary
card_to_json(card(Rank, Suit), _{rank: RankStr, suit: SuitStr}) :-
    atom_string(Rank, RankStr),
    atom_string(Suit, SuitStr).

% Initialize deck
init_deck :-
    retractall(deck(_)),
    ranks(Ranks),
    suits(Suits),
    findall(card(Rank, Suit), (member(Rank, Ranks), member(Suit, Suits)), Deck),
    assert(deck(Deck)).

% Draw N random cards from deck
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

% Draw one card from deck and display it
draw_one_card(Player, Card) :-
    deck(Deck),
    (Deck \= [] ->
        random_member(Card, Deck),
        delete(Deck, Card, NewDeck),
        retract(deck(Deck)),
        assert(deck(NewDeck)),
        hand(Player, Hand),
        append(Hand, [Card], NewHand),
        retract(hand(Player, Hand)),
        assert(hand(Player, NewHand)),
        (\+ http_mode -> format('~w drew: ~w~n', [Player, Card]) ; true),
        (Card = card(Rank, _) -> check_set(Player, Rank) ; true)
    ; 
        (\+ http_mode -> format('Deck is empty! No card drawn for ~w.~n', [Player]) ; true),
        Card = none).

% Initialize game
init_game :-
    retractall(hand(_, _)),
    retractall(score(_, _)),
    retractall(turn(_)),
    retractall(difficulty(_)),
    retractall(game_state(_)),
    retractall(requested_rank(_, _)),
    init_deck,
    draw_cards(7, PlayerHand),
    draw_cards(7, BotHand),
    assert(hand(player, PlayerHand)),
    assert(hand(bot, BotHand)),
    assert(score(player, 0)),
    assert(score(bot, 0)),
    random(0, 2, Turn),
    (Turn = 0 -> assert(turn(player)) ; assert(turn(bot))),
    assert(difficulty(easy)),
    assert(game_state(active)).

% Show player's hand
show_hand(Player) :-
    hand(Player, Hand),
    write('Your hand: '), nl,
    (Hand = [] -> write('Empty') ; maplist(write_card, Hand)),
    nl.

write_card(card(Rank, Suit)) :-
    format('~w of ~w~n', [Rank, Suit]).

% Check for a set of 4 cards of the same rank
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
        (\+ http_mode -> format('~w completed a set (~w)! Point added. Score: ~w~n', [Player, Rank, NewScore]) ; true),
        (NewScore = 3 -> end_game(Player) ; true)
    ; true).

% Transfer cards from one player to another
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
        (\+ http_mode -> format('~w gave ~w ~w card(s) of rank ~w~n', [From, To, NumCards, Rank]) ; true),
        check_set(To, Rank),
        true
    ; 
        false).

% Request a card
request_card(Player, Opponent, Rank, Result) :-
    hand(Player, Hand),
    (member(card(Rank, _), Hand) ->
        (transfer_cards(Rank, Opponent, Player) -> 
            Result = continue 
        ; 
            draw_one_card(Player, _),
            Result = switch)
    ; 
        Result = retry).

% Switch turn
switch_turn(Player, Opponent) :-
    retract(turn(Player)),
    assert(turn(Opponent)).

% Bot move (easy level: random choice)
bot_move_easy(Bot, Player, Result) :-
    hand(Bot, Hand),
    (Hand \= [] ->
        findall(Rank, member(card(Rank, _), Hand), Ranks),
        random_member(Rank, Ranks),
        (\+ http_mode -> format('Bot requests: ~w~n', [Rank]) ; true),
        request_card(Bot, Player, Rank, BotResult),
        (BotResult = switch ->
            (\+ http_mode -> format('~w has no cards of rank ~w. Turn passes.~n', [Player, Rank]) ; true),
            Result = switch
        ; BotResult = continue ->
            Result = continue
        ; % BotResult = retry, select a valid rank
            findall(ValidRank, (member(card(ValidRank, _), Hand), ValidRank \= Rank), ValidRanks),
            (ValidRanks \= [] ->
                random_member(NewRank, ValidRanks),
                (\+ http_mode -> format('Bot doesn\'t have cards of rank ~w! Trying ~w.~n', [Rank, NewRank]) ; true),
                request_card(Bot, Player, NewRank, NewResult),
                (NewResult = switch ->
                    (\+ http_mode -> format('~w has no cards of rank ~w. Turn passes.~n', [Player, NewRank]) ; true),
                    Result = switch
                ; Result = continue)
            ; Result = switch))
    ; 
        (\+ http_mode -> format('Bot has no cards! Turn passes.~n', []) ; true),
        Result = switch).

% Bot move (hard level: choose rank with most cards)
bot_move_hard(Bot, Player, Result) :-
    hand(Bot, Hand),
    (Hand \= [] ->
        findall(Rank-Count, 
                (aggregate_all(count, member(card(Rank, _), Hand), Count), Count > 0), 
                RankCounts),
        sort(2, @>=, RankCounts, Sorted),
        Sorted = [BestRank-_|_],
        (\+ http_mode -> format('Bot requests: ~w~n', [BestRank]) ; true),
        request_card(Bot, Player, BestRank, BotResult),
        (BotResult = switch ->
            (\+ http_mode -> format('~w has no cards of rank ~w. Turn passes.~n', [Player, BestRank]) ; true),
            Result = switch
        ; BotResult = continue ->
            Result = continue
        ; % BotResult = retry, select a valid rank
            findall(ValidRank, (member(card(ValidRank, _), Hand), ValidRank \= BestRank), ValidRanks),
            (ValidRanks \= [] ->
                random_member(NewRank, ValidRanks),
                (\+ http_mode -> format('Bot doesn\'t have cards of rank ~w! Trying ~w.~n', [BestRank, NewRank]) ; true),
                request_card(Bot, Player, NewRank, NewResult),
                (NewResult = switch ->
                    (\+ http_mode -> format('~w has no cards of rank ~w. Turn passes.~n', [Player, NewRank]) ; true),
                    Result = switch
                ; Result = continue)
            ; Result = switch))
    ; 
        (\+ http_mode -> format('Bot has no cards! Turn passes.~n', []) ; true),
        Result = switch).

% Bot move (medium level: prioritize ranks requested by player)
bot_move_medium(Bot, Player, Result) :-
    hand(Bot, Hand),
    (Hand \= [] ->
        findall(Rank, member(card(Rank, _), Hand), Ranks),
        findall(RequestedRank, requested_rank(Player, RequestedRank), PlayerRequests),
        findall(Rank, (member(Rank, Ranks), member(Rank, PlayerRequests)), ValidRanks),
        (ValidRanks \= [] ->
            random_member(Rank, ValidRanks)
        ; 
            random_member(Rank, Ranks)),
        (\+ http_mode -> format('Bot requests: ~w~n', [Rank]) ; true),
        request_card(Bot, Player, Rank, BotResult),
        (BotResult = switch ->
            (\+ http_mode -> format('~w has no cards of rank ~w. Turn passes.~n', [Player, Rank]) ; true),
            Result = switch
        ; BotResult = continue ->
            Result = continue
        ; % BotResult = retry, select a valid rank
            findall(ValidRank, (member(card(ValidRank, _), Hand), ValidRank \= Rank), ValidRanksRetry),
            (ValidRanksRetry \= [] ->
                random_member(NewRank, ValidRanksRetry),
                (\+ http_mode -> format('Bot doesn\'t have cards of rank ~w! Trying ~w.~n', [Rank, NewRank]) ; true),
                request_card(Bot, Player, NewRank, NewResult),
                (NewResult = switch ->
                    (\+ http_mode -> format('~w has no cards of rank ~w. Turn passes.~n', [Player, NewRank]) ; true),
                    Result = switch
                ; Result = continue)
            ; Result = switch))
    ; 
        (\+ http_mode -> format('Bot has no cards! Turn passes.~n', []) ; true),
        Result = switch).

% Bot move based on difficulty
bot_move(Bot, Player, Result) :-
    difficulty(Difficulty),
    (Difficulty = easy -> bot_move_easy(Bot, Player, Result) ;
     Difficulty = medium -> bot_move_medium(Bot, Player, Result) ;
     bot_move_hard(Bot, Player, Result)).

% End game
end_game(Winner) :-
    retract(game_state(active)),
    assert(game_state(finished)),
    score(player, PlayerScore),
    score(bot, BotScore),
    (\+ http_mode -> 
        format('Game over! ~w wins!~n', [Winner]),
        format('Score: Player - ~w, Bot - ~w~n', [PlayerScore, BotScore]),
        show_hand(player)
    ; true).

% HTTP Handlers
:- http_handler('/move', handle_move, []).
:- http_handler('/status', handle_status, []).
:- http_handler('/init', handle_init, []).
:- http_handler('/set_difficulty', handle_set_difficulty, []).

% Handle POST /move
handle_move(Request) :-
    assert(http_mode),
    catch(
        (game_state(State) ->
            (State = finished ->
                score(player, PlayerScore),
                score(bot, BotScore),
                (PlayerScore = 3 -> Winner = player ; BotScore = 3 -> Winner = bot ; Winner = null),
                reply_json_dict(_{error: "Game is already finished", winner: Winner}, [status(400)])
            ; 
                http_read_json_dict(Request, Data),
                (get_dict(rank, Data, RankStr) ->
                    atom_string(RankAtom, RankStr),
                    ranks(Ranks),
                    (member(RankAtom, Ranks) ->
                        (turn(player) ->
                            (RankAtom \= dummy -> assertz(requested_rank(player, RankAtom)) ; true),
                            process_player_move(RankAtom, Result),
                            prepare_response(Result, Response),
                            reply_json_dict(Response)
                        ; 
                            reply_json_dict(_{error: "Not player's turn"}, [status(400)]))
                    ; 
                        reply_json_dict(_{error: "Invalid rank"}, [status(400)]))
                ; 
                    reply_json_dict(_{error: "Missing rank field"}, [status(400)])))
        ; 
            reply_json_dict(_{error: "Game not initialized"}, [status(400)])),
        Error,
        (term_string(Error, ErrorStr),
         reply_json_dict(_{error: "Internal server error", message: ErrorStr}, [status(500)]))
    ),
    retractall(http_mode).

% Process player's move and execute bot's move if needed
process_player_move(Rank, Result) :-
    request_card(player, bot, Rank, PlayerResult),
    (PlayerResult = continue ->
        Result = continue
    ; PlayerResult = switch ->
        switch_turn(player, bot),
        bot_move(bot, player, BotResult),
        (BotResult = switch ->
            switch_turn(bot, player),
            Result = switch
        ; BotResult = continue ->
            Result = switch % Bot succeeded, turn stays with bot
        ; % BotResult = retry (should not happen with fixed bot logic)
            Result = switch)
    ; PlayerResult = retry ->
        Result = retry).

% Prepare HTTP response
prepare_response(Result, Response) :-
    score(player, PlayerScore),
    score(bot, BotScore),
    hand(player, PlayerHand),
    maplist(card_to_json, PlayerHand, JsonHand),
    game_state(State),
    (State = finished ->
        (score(player, 3) -> Winner = player ; score(bot, 3) -> Winner = bot ; Winner = null),
        Response = _{result: Result, player_score: PlayerScore, bot_score: BotScore, player_hand: JsonHand, game_state: "finished", winner: Winner}
    ; 
        Response = _{result: Result, player_score: PlayerScore, bot_score: BotScore, player_hand: JsonHand, game_state: "active"}).

% Handle GET /status
handle_status(_Request) :-
    assert(http_mode),
    score(player, PlayerScore),
    score(bot, BotScore),
    game_state(State),
    (State = finished ->
        (PlayerScore = 3 -> Winner = player ; BotScore = 3 -> Winner = bot ; Winner = null)
    ; 
        Winner = null),
    reply_json_dict(_{player_score: PlayerScore, bot_score: BotScore, winner: Winner}),
    retractall(http_mode).

% Handle POST /init
handle_init(_Request) :-
    assert(http_mode),
    init_game,
    reply_json_dict(_{status: "Game initialized"}),
    retractall(http_mode).

% Handle POST /set_difficulty
handle_set_difficulty(Request) :-
    assert(http_mode),
    catch(
        (http_read_json_dict(Request, Data),
         (get_dict(difficulty, Data, DifficultyStr) ->
             atom_string(DifficultyAtom, DifficultyStr),
             (member(DifficultyAtom, [easy, medium, hard]) ->
                 set_difficulty(DifficultyAtom),
                 reply_json_dict(_{status: "Difficulty set", difficulty: DifficultyAtom})
             ; 
                 reply_json_dict(_{error: "Invalid difficulty, use 'easy', 'medium', or 'hard'"}, [status(400)]))
         ; 
             reply_json_dict(_{error: "Missing 'difficulty' field"}, [status(400)]))),
        Error,
        (term_string(Error, ErrorStr),
         reply_json_dict(_{error: "Invalid JSON format", message: ErrorStr}, [status(400)]))
    ),
    retractall(http_mode).

% Start HTTP server
start_server :-
    http_server(http_dispatch, [port(8080)]),
    format('Server running at http://localhost:8080~n', []).

% Main game loop (for console mode)
game_loop :-
    game_state(State),
    (State = finished -> 
        true % Game is over, stop the loop
    ; 
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
             Result = switch -> switch_turn(bot, player), game_loop))
    ).

% Player move (for console mode)
player_move(Result) :-
    write('Enter rank (ace, two, ..., king): '),
    read(Rank),
    ranks(Ranks),
    (member(Rank, Ranks) ->
        assertz(requested_rank(player, Rank)),
        request_card(player, bot, Rank, PlayerResult),
        (PlayerResult = retry ->
            format('You don\'t have cards of rank ~w! Try again.~n', [Rank]),
            Result = retry
        ; PlayerResult = switch ->
            format('Bot has no cards of rank ~w. Turn passes.~n', [Rank]),
            Result = switch
        ; Result = continue)
    ; 
        format('Invalid rank! Try again.~n', []),
        Result = retry).

% Set difficulty level
set_difficulty(Level) :-
    member(Level, [easy, medium, hard]),
    retractall(difficulty(_)),
    assert(difficulty(Level)).

% Start game (console mode)
play :-
    write('Welcome to "Take a Card, Mr. Pig"!'), nl,
    write('Choose difficulty level (easy/medium/hard): '),
    read(Difficulty),
    (set_difficulty(Difficulty) ->
        format('Difficulty level set: ~w~n', [Difficulty]),
        init_game,
        turn(First),
        format('First turn: ~w~n', [First]),
        game_loop
    ; 
        write('Invalid difficulty level! Try again (easy/medium/hard).'), nl,
        play).