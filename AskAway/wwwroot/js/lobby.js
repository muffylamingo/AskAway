
const screens = {
    welcome: document.getElementById('welcomeScreen'),
    join: document.getElementById('joinScreen'),
    lobby: document.getElementById('lobbyScreen'),
    game: document.getElementById('gameScreen'),
    results: document.getElementById('resultsScreen')
};

const buttons = {
    createRoom: document.getElementById('createRoomBtn'),
    joinRoom: document.getElementById('joinRoomBtn'),
    confirmJoin: document.getElementById('confirmJoinBtn'),
    backToWelcome: document.getElementById('backToWelcomeBtn'),
    startGame: document.getElementById('startGameBtn'),
    leaveLobby: document.getElementById('leaveLobbyBtn'),
    nextRound: document.getElementById('nextRoundBtn'),
    playAgain: document.getElementById('playAgainBtn')
};

const inputs = {
    playerName: document.getElementById('playerName'),
    joinPlayerName: document.getElementById('joinPlayerName'),
    roomCode: document.getElementById('roomCode')
};

const lobbyElements = {
    displayRoomCode: document.getElementById('displayRoomCode'),
    playersList: document.getElementById('playersList'),
    waitingIndicator: document.getElementById('waitingIndicator')
};

const gameElements = {
    kingName: document.getElementById('currentKingName'),
    questionText: document.getElementById('questionText'),
    timerProgress: document.getElementById('timerProgress'),
    timerText: document.getElementById('timerText'),
    optionButtons: document.querySelectorAll('.option-btn'),
    waitingState: document.getElementById('waitingForAnswers'),
    answeredCount: document.getElementById('answeredCount'),
    totalPlayers: document.getElementById('totalPlayers')
};

let isHost = false;

// --- 2. SIGNALR BAĞLANTISI ---
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/gameHub")
    .build();

connection.start().then(() => {
    console.log("SignalR Bağlantısı Başarılı!");
}).catch(err => console.error("Bağlantı Hatası: ", err.toString()));

// --- 3. SUNUCUDAN GELEN SİNYALLER ---

connection.on("RoomCreated", (roomCode) => {
    if (lobbyElements.displayRoomCode) lobbyElements.displayRoomCode.textContent = roomCode;
    if (buttons.startGame) buttons.startGame.style.display = 'flex';
    if (lobbyElements.playersList) lobbyElements.playersList.innerHTML = '';
    addPlayerToList(inputs.playerName.value.trim(), true);
    showScreen('lobby');
});

connection.on("UpdatePlayerList", (players) => {
    if (lobbyElements.playersList) {
        lobbyElements.playersList.innerHTML = '';
        players.forEach(p => addPlayerToList(p.name, p.isHost));
    }
});

connection.on("Error", (message) => {
    alert(message);
    showScreen('welcome');
});

connection.on("GameStarted", (data) => {
    if (gameElements.kingName) gameElements.kingName.textContent = data.kingName;
    if (gameElements.questionText) gameElements.questionText.innerHTML = data.questionText;

    gameElements.optionButtons.forEach((btn, index) => {
        const optionText = btn.querySelector('.option-text');
        if (optionText) optionText.textContent = data.options[index];
        btn.classList.remove('selected');
        btn.disabled = false;
    });

    showScreen('game');
});

connection.on("UpdateAnswerCount", (answered, total) => {
    if (gameElements.answeredCount) gameElements.answeredCount.textContent = answered;
    if (gameElements.totalPlayers) gameElements.totalPlayers.textContent = total;
});

connection.on("ShowResults", (data) => {
    console.log("Sonuçlar Verisi:", data);

    const resultsTitle = document.getElementById('resultsTitle');
    const kingChoice = document.getElementById('kingChoice');
    const scoreboardList = document.getElementById('scoreboardList');

    if (kingChoice) kingChoice.textContent = "Şık " + data.correctAnswer;

    if (scoreboardList) {
        scoreboardList.innerHTML = '';
        data.playerResults.forEach(p => {
            const li = document.createElement('li');
            li.className = `score-item ${p.isCorrect ? 'correct' : ''} ${p.isKing ? 'king' : ''}`;

            let icon = p.isKing ? "👑" : (p.isCorrect ? "✅" : "❌");
            let pointsText = p.isKing ? "" : (p.isCorrect ? "+5" : "0");

            li.innerHTML = `
                <div class="score-left">
                    <span class="score-icon">${icon}</span>
                    <span class="player-name">${p.playerName}</span>
                </div>
                <div class="score-right">
                    <span class="score-points">${pointsText}</span>
                    <span class="score-total">${p.score} Puan</span>
                </div>
            `;
            scoreboardList.appendChild(li);
        });
    }

    if (resultsTitle) {
        if (data.isGameOver) {
            resultsTitle.innerHTML = "🎉 OYUN BİTTİ!";

            // Oyun bittiyse: Sadece Host 'Play Again' butonunu görür, 'Next Round' herkeste gizlenir
            if (isHost) {
                buttons.nextRound.style.display = 'none';
                buttons.playAgain.style.display = 'flex';
            } else {
                buttons.nextRound.style.display = 'none';
                buttons.playAgain.style.display = 'none';
                // Host olmayanlara bir bilgi mesajı eklenebilir
                resultsTitle.innerHTML += "<br><small style='font-size:0.5em'>Hostun oyunu yeniden başlatması bekleniyor...</small>";
            }
        } else {
            resultsTitle.textContent = "Tur Sonucu";

            // Tur devam ediyorsa: Sadece Host 'Next Round' butonunu görür
            if (isHost) {
                buttons.nextRound.style.display = 'flex';
                buttons.playAgain.style.display = 'none';
            } else {
                buttons.nextRound.style.display = 'none';
                buttons.playAgain.style.display = 'none';
            }
        }
    }

    if (gameElements.waitingState) gameElements.waitingState.style.display = 'none';
    showScreen('results');
});

// --- 4. BUTON TIKLAMA OLAYLARI ---

if (buttons.createRoom) {
    buttons.createRoom.addEventListener('click', () => {
        const playerName = inputs.playerName.value.trim();
        if (!playerName) return alert('Lütfen adınızı girin!');
        isHost = true;
        connection.invoke("CreateRoom", playerName).catch(err => console.error(err));
    });
}

if (buttons.joinRoom) {
    buttons.joinRoom.addEventListener('click', () => {
        const playerName = inputs.playerName.value.trim();
        if (!playerName) return alert('Lütfen adınızı girin!');
        inputs.joinPlayerName.value = playerName;
        showScreen('join');
    });
}

if (buttons.confirmJoin) {
    buttons.confirmJoin.addEventListener('click', () => {
        const playerName = inputs.joinPlayerName.value.trim();
        const roomCode = inputs.roomCode.value.trim().toUpperCase();
        if (!playerName || !roomCode || roomCode.length !== 4) return alert('Eksik bilgi!');
        isHost = false;
        if (buttons.startGame) buttons.startGame.style.display = 'none';
        if (lobbyElements.displayRoomCode) lobbyElements.displayRoomCode.textContent = roomCode;
        showScreen('lobby');
        connection.invoke("JoinRoom", roomCode, playerName).catch(err => console.error(err));
    });
}

if (buttons.backToWelcome) {
    buttons.backToWelcome.addEventListener('click', () => showScreen('welcome'));
}

if (buttons.startGame) {
    buttons.startGame.addEventListener('click', () => {
        const roomCode = lobbyElements.displayRoomCode.textContent;
        connection.invoke("StartGame", roomCode).catch(err => console.error(err));
    });
}

if (buttons.nextRound) {
    buttons.nextRound.addEventListener('click', () => {
        const roomCode = lobbyElements.displayRoomCode.textContent;
        connection.invoke("StartGame", roomCode).catch(err => console.error(err));
    });
}

// --- 5. OYUN EKRANI FONKSİYONLARI ---

gameElements.optionButtons.forEach(btn => {
    btn.addEventListener('click', () => {
        gameElements.optionButtons.forEach(b => b.disabled = true);
        btn.classList.add('selected');
        if (gameElements.waitingState) gameElements.waitingState.style.display = 'block';

        const selectedOption = btn.dataset.option;
        const roomCode = lobbyElements.displayRoomCode.textContent;
        const playerName = inputs.playerName.value.trim() || inputs.joinPlayerName.value.trim();

        connection.invoke("SubmitAnswer", roomCode, playerName, selectedOption).catch(err => console.error(err));
    });
});

// --- 6. YARDIMCI FONKSİYONLAR ---

function showScreen(screenName) {
    Object.keys(screens).forEach(key => {
        if (screens[key]) screens[key].classList.remove('active');
    });
    if (screens[screenName]) {
        screens[screenName].classList.add('active');
    } else {
        console.error("Ekran bulunamadı:", screenName);
    }
}

function addPlayerToList(playerName, isCurrentHost = false) {
    if (lobbyElements.playersList) {
        const li = document.createElement('li');
        li.textContent = playerName + (isCurrentHost ? ' 👑 (Host)' : '');
        lobbyElements.playersList.appendChild(li);
    }
}

if (inputs.roomCode) {
    inputs.roomCode.addEventListener('input', (e) => e.target.value = e.target.value.toUpperCase());
}