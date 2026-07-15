# Déployer le chatbot panel (étape 3)

Bot **séparé** du `tweakslab-bot` principal. Repo : `tweakslab-panel-bot`.

## 1. Pousser le bot sur GitHub

```powershell
cd "C:\Users\TweaksLab User\Projects\tweakslab-panel-bot"
git add .
git commit -m "Initial panel bot API"
# Crée un repo vide mid0-ctrl/tweakslab-panel-bot sur GitHub, puis :
git remote add origin https://github.com/mid0-ctrl/tweakslab-panel-bot.git
git push -u origin main
```

## 2. Railway — nouveau service

1. [railway.app](https://railway.app) → **New Project** → **Deploy from GitHub**
2. Choisis **`mid0-ctrl/tweakslab-panel-bot`** (pas `tweakslab-bot`)
3. Variables d'environnement :

| Variable | Valeur |
|----------|--------|
| `ANTHROPIC_API_KEY` | Ta clé Claude (idéalement dédiée au panel) |
| `PANEL_API_KEY` | **Identique** à `PanelChatApiKey` dans `PanelEndpoints.cs` (ne pas committer ailleurs) |
| `UPSTASH_REDIS_REST_URL` | URL REST Upstash *(recommandé — rate limits partagés)* |
| `UPSTASH_REDIS_REST_TOKEN` | Token REST Upstash |
| `RATE_LIMIT_IP_PER_MIN` | `8` |
| `RATE_LIMIT_SESSION_PER_MIN` | `6` |
| `RATE_LIMIT_GLOBAL_PER_MIN` | `120` |
| `SESSION_COOLDOWN_MS` | `3000` |
| `SESSION_DAILY_LIMIT` | `40` |
| `IP_DAILY_LIMIT` | `80` |
| `GLOBAL_DAILY_LIMIT` | `4000` |
| `MAX_CONCURRENT` | `20` |
| `SECURITY_WEBHOOK_URL` | *(optionnel — alertes Discord)* |

4. Settings → **Networking** → **Generate Domain**
5. Mets à jour `PANEL_API_KEY` sur Railway avec la valeur de `PanelEndpoints.cs` (rotation : changer les deux en même temps)
6. Si l'URL diffère de celle ci-dessous, mets à jour `ChatBaseUrl` dans `PanelEndpoints.cs`

URL attendue : `https://web-production-8a75c4.up.railway.app`

## Upstash Redis (recommandé)

1. [console.upstash.com](https://console.upstash.com) → créer une base Redis
2. Copier **REST URL** et **REST Token** dans Railway (`UPSTASH_REDIS_REST_URL`, `UPSTASH_REDIS_REST_TOKEN`)
3. Sans Redis, les rate limits restent en mémoire (reset au redémarrage, non partagés entre replicas)

Vérification :

```powershell
Invoke-RestMethod https://web-production-8a75c4.up.railway.app/health
# → { ok: true, service: "tweakslab-panel-bot" }
```

## 3. Panel — déjà branché

## 4. Tester

```powershell
# Bot local (optionnel)
cd tweakslab-panel-bot
npm install
npm run dev

curl http://localhost:3000/health
```

Puis lance le panel → **Assistance** → envoie un message.

## Comportement Tetsuya (prompt)

Inspire du bot Discord/site (prix, vente consultative) + logique panel :

| Situation | Reponse |
|-----------|---------|
| Question score / perf | Astuces + Recommandations + GPU dans le panel, plafond ~20% gratuit, puis opti payante + lien |
| Probleme PC simple | Conseil concret dans le logiciel |
| Probleme trop dur (BIOS, OC, bug persistant) | Aide prudente + bouton Discord |
| Client frustre | Zero vente, aide + Discord |
| Conseils gratuits confirmes | Felicite, +20% boost session, closing vers opti des 24,99€ |

Reponse API : `{ reply, cta?, ticketCta?, scoreBoost? }` — le panel affiche les liens cliquables sous le message.

## Isolation garantie

- Service Railway **différent** du bot principal
- Repo **différent**
- Aucune base partagée
- Si ce bot est compromis → le bot principal reste intact
