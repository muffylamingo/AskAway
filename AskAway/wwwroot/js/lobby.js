
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

// =======================================================
// 1. OYUN BAŞLADIĞINDA (KRAL KONTROLÜ DÜZELTİLDİ)
// =======================================================
connection.on("GameStarted", (data) => {
    console.log("GameStarted İçin Gelen Veri:", data);

    // 🌟 BÜYÜK DÜZELTME BURADA: Sadece gizleme, DOM'dan (HTML'den) tamamen SİL!
    const oldKingContainer = document.getElementById('kingSelectionContainer');
    if (oldKingContainer) {
        oldKingContainer.remove(); // Kökten yok ediyoruz!
    }

    if (gameElements.kingName) gameElements.kingName.textContent = data.kingName;
    if (gameElements.questionText) gameElements.questionText.innerHTML = data.questionText;

    // JavaScript artık eski kral kutusu silindiği için GERÇEK A-B-C kutusunu bulacak
    const optionsContainer = document.querySelector('.options-container');
    const textInputContainer = document.getElementById('textInputContainer');

    // 🌟 1. BÜYÜK DÜZELTME: BEN KRAL MIYIM? 
    // İsmi kesin olarak Input'ların içinden (.value) alıyoruz.
    let myName = document.getElementById('playerName').value.trim();
    if (myName === "") myName = document.getElementById('joinPlayerName').value.trim();

    // Gerçek oyuncu adıyla, sunucudan gelen Kral adı aynı mı?
    const isKing = (data.kingName === myName);

    if (data.questionType == 2) {
        if (optionsContainer) optionsContainer.style.display = 'none';

        if (isKing) {
            // KRALSAM KUTUYU GİZLE!
            if (textInputContainer) textInputContainer.style.display = 'none';
            if (gameElements.waitingState) {
                gameElements.waitingState.innerHTML = "<p>👑 Oyuncuların komik cevaplar yazması bekleniyor...</p>";
                gameElements.waitingState.style.display = 'block';
            }
        } else {
            // OYUNCUYSAM KUTUYU AÇ!
            if (textInputContainer) textInputContainer.style.display = 'block';
            const textInput = document.getElementById('openEndedAnswer');
            if (textInput) textInput.value = '';
            if (gameElements.waitingState) gameElements.waitingState.style.display = 'none';
        }
    } else {
        if (optionsContainer) optionsContainer.style.display = 'flex';
        if (textInputContainer) textInputContainer.style.display = 'none';

        gameElements.optionButtons.forEach((btn, index) => {
            if (data.options && index < data.options.length) {
                const optionText = btn.querySelector('.option-text');
                if (optionText) optionText.textContent = data.options[index];

                btn.classList.remove('selected');
                btn.disabled = false;
                btn.style.display = 'block';
            } else {
                btn.style.display = 'none';
            }
        });

        if (gameElements.waitingState) gameElements.waitingState.style.display = 'none';
    }

    showScreen('game');
});

// =======================================================
// 2. GÖNDER BUTONU (GİZLİ EKRAN OKUMA DÜZELTİLDİ)
// =======================================================
const submitOpenEndedBtn = document.getElementById('submitOpenEndedBtn');
if (submitOpenEndedBtn) {
    submitOpenEndedBtn.onclick = () => {
        const answerInput = document.getElementById('openEndedAnswer');
        const answer = answerInput.value.trim();

        if (answer !== "") {

            // 🌟 2. BÜYÜK DÜZELTME: textContent kullanarak gizli div'den yazıyı okuyoruz!
            let exactRoomCode = document.getElementById('displayRoomCode').textContent.trim();
            // Eğer lobide değil de join ekranındaysa inputtan almayı deneriz:
            if (exactRoomCode === "" || exactRoomCode === "----") {
                exactRoomCode = document.getElementById('roomCode').value.trim();
            }

            let exactPlayerName = document.getElementById('playerName').value.trim();
            if (exactPlayerName === "") {
                exactPlayerName = document.getElementById('joinPlayerName').value.trim();
            }

            console.log("SON KONTROL -> Oda:", exactRoomCode, "Oyuncu:", exactPlayerName, "Cevap:", answer);

            if (exactRoomCode === "" || exactRoomCode === "----") {
                alert("Oda kodu hala bulunamadı! Lütfen oyunu yenileyip baştan oda kur.");
                return;
            }

            connection.invoke("SubmitAnswer", exactRoomCode, exactPlayerName, answer)
                .then(() => {
                    document.getElementById('textInputContainer').style.display = 'none';
                    if (gameElements.waitingState) {
                        gameElements.waitingState.innerHTML = "<p>Cevap Gönderildi! Diğerleri bekleniyor...</p>";
                        gameElements.waitingState.style.display = 'block';
                    }
                })
                .catch(err => {
                    console.error("Gönderme Hatası:", err);
                    alert("Cevap gönderilirken sunucuda bir hata oluştu!");
                });
        } else {
            alert("Lütfen bir cevap yazın!");
        }
    };
}
connection.on("UpdateAnswerCount", (answered, total) => {
    if (gameElements.answeredCount) gameElements.answeredCount.textContent = answered;
    if (gameElements.totalPlayers) gameElements.totalPlayers.textContent = total;
});

connection.on("ShowResults", (data) => {
    console.log("Sonuçlar Verisi:", data);

    const resultsTitle = document.getElementById('resultsTitle');
    const kingChoice = document.getElementById('kingChoice');
    const scoreboardList = document.getElementById('scoreboardList');

    // 1. Sorun Çözümü: Kralın Seçimindeki çift harfi sildik, sadece metni gösteriyoruz
    if (kingChoice) {
        kingChoice.innerHTML = `👑 Kralın Seçimi: <strong>${data.correctAnswerContent}</strong>`;
    }

    if (scoreboardList) {
        scoreboardList.innerHTML = '';
        data.playerResults.forEach(p => {
            const li = document.createElement('li');
            li.className = `score-item ${p.isCorrect ? 'correct' : ''} ${p.isKing ? 'king' : ''}`;

            let icon = p.isKing ? "👑" : (p.isCorrect ? "✅" : "❌");
            let pointsText = p.isKing ? "" : (p.isCorrect ? "+5" : "0");

            // 2. Sorun Çözümü: Artık C#'tan gelen .answerText'i kullanıyoruz (Örn: Tutku Seçti)
            let playerVoteText = p.isKing ? "Karar Verici" : `(${p.answerText} Seçti)`;

            // HTML Şablonu (Ters tırnaklara dikkat!)
            li.innerHTML = `
                <div class="score-left">
                    <span class="score-icon">${icon}</span>
                    <span class="player-name">${p.playerName} <small style="color: #888; margin-left: 5px;">${playerVoteText}</small></span>
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

            if (isHost) {
                buttons.nextRound.style.display = 'none';
                buttons.playAgain.style.display = 'flex';
            } else {
                buttons.nextRound.style.display = 'none';
                buttons.playAgain.style.display = 'none';
                resultsTitle.innerHTML += "<br><small style='font-size:0.5em'>Hostun oyunu yeniden başlatması bekleniyor...</small>";
            }
        } else {
            resultsTitle.textContent = "Tur Sonucu";

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


// ==========================================================
// --- BÜYÜK FİNAL: KRAL SEÇİM EKRANI (Mod 2 Aşama 2) ---
// ==========================================================
connection.on("ShowKingSelection", (anonymousAnswers) => {
    console.log("Kral Seçim Ekranı Geldi! Gelen Cevaplar:", anonymousAnswers);

    if (gameElements.waitingState) gameElements.waitingState.style.display = 'none';
    const textInputContainer = document.getElementById('textInputContainer');
    if (textInputContainer) textInputContainer.style.display = 'none';

    const classicOptions = document.querySelector('.options-container');
    if (classicOptions) classicOptions.style.display = 'none';

    let kingContainer = document.getElementById('kingSelectionContainer');
    if (!kingContainer) {
        kingContainer = document.createElement('div');
        kingContainer.id = 'kingSelectionContainer';
        kingContainer.className = 'options-container';

        const questionElement = document.getElementById('questionText');
        if (questionElement) {
            questionElement.parentNode.insertBefore(kingContainer, questionElement.nextSibling);
        }
    }

    kingContainer.innerHTML = '';
    kingContainer.style.display = 'flex';

    // 🌟 DÜZELTME: Kral ben miyim kontrolünü GÜVENLİ yoldan yapıyoruz!
    let exactPlayerName = document.getElementById('playerName').value.trim();
    if (exactPlayerName === "") {
        exactPlayerName = document.getElementById('joinPlayerName').value.trim();
    }

    const currentKing = document.getElementById('currentKingName').textContent.trim();
    const amIKing = (currentKing === exactPlayerName);

    // Başlık daha önce eklenmediyse ekle
    if (gameElements.questionText && !gameElements.questionText.innerHTML.includes("Karar Vakti")) {
        gameElements.questionText.innerHTML += "<br><span style='font-size: 1.2rem; color: #ffd700;'>👑 Karar Vakti! En komiğini seç!</span>";
    }

    anonymousAnswers.forEach(answer => {
        const btn = document.createElement('button');
        btn.className = 'option-btn';
        btn.innerHTML = `<span class="option-text">${answer}</span>`;

        if (amIKing) {
            // 👑 EĞER KRALSAM: KİLİTLER AÇIK!
            btn.disabled = false;
            btn.onclick = () => {
                // Oda kodunu da güvenli çekiyoruz
                let exactRoomCode = document.getElementById('displayRoomCode').textContent.trim();
                if (exactRoomCode === "" || exactRoomCode === "----") {
                    exactRoomCode = document.getElementById('roomCode').value.trim();
                }

                console.log("Kral Seçim Yaptı -> Oda:", exactRoomCode, "Kral:", exactPlayerName, "Seçilen:", answer);

                // Seçilen cevabı sunucuya fırlat!
                connection.invoke("SubmitAnswer", exactRoomCode, exactPlayerName, answer)
                    .catch(err => {
                        console.error("Kral Seçim Hatası:", err);
                        alert("Seçim gönderilirken hata oluştu!");
                    });

                // Seçtikten sonra çift tıklamayı önlemek için butonları kilitle
                kingContainer.querySelectorAll('button').forEach(b => b.disabled = true);
            };
        } else {
            // 👤 NORMAL OYUNCUYSAM: Butonlar kilitli kalır, sadece izlerim
            btn.disabled = true;
            btn.style.cursor = 'not-allowed';
            btn.style.opacity = '0.8';
        }

        kingContainer.appendChild(btn);
    });
});