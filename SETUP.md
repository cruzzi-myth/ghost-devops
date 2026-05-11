# Ghost DevOps — Setup Guide

## Step 1: Create the GitHub Repo

1. Go to https://github.com/new
2. Name it `ghost-devops`
3. Set to **Public** (required for the portfolio link)
4. Don't initialize with README (we have one)
5. Run locally:

```bash
cd ghost-devops
git init
git add .
git commit -m "feat: initial Ghost DevOps scaffold"
git branch -M main
git remote add origin https://github.com/cruzzi-myth/ghost-devops.git
git push -u origin main
```

## Step 2: Set Your Secrets

Create a `.env` file (never commit this):

```env
ANTHROPIC_API_KEY=sk-ant-...
GITHUB_TOKEN=ghp_...
GITHUB_OWNER=cruzzi-myth
GITHUB_REPO=ghost-devops
```

Add the same secrets to GitHub repo → Settings → Secrets → Actions:
- `ANTHROPIC_API_KEY`
- `GITHUB_TOKEN`

## Step 3: Run the Stack

```bash
docker compose up -d
```

Services:
| Service | URL |
|---|---|
| Target App (the broken one) | http://localhost:5001 |
| Ghost Gateway | http://localhost:5000/swagger |
| Python Brain | http://localhost:8000/docs |
| Prometheus | http://localhost:9090 |
| Alertmanager | http://localhost:9093 |
| Dashboard | http://localhost:3000 |

## Step 4: Trigger the Pipeline

```bash
# Hit the memory leak endpoint 15+ times to push memory past 90%
for i in {1..20}; do curl http://localhost:5001/leak; done

# Watch the Ghost pipeline in the dashboard
open http://localhost:3000
```

## Step 5: Link to Your Portfolio

1. Open your portfolio repo: https://github.com/cruzzi-myth/Professional-portfolio
2. Find where your other project cards are defined in the HTML
3. Copy the contents of `ghost-devops-portfolio-card.html` from your Ghost DEVOps folder
4. Paste it alongside your other project cards
5. Update the GitHub link in the card once the repo is live

## Step 6: Update the Gateway Config

Open `src/GhostDevOps.Gateway/appsettings.json` and fill in:

```json
{
  "GitHub": {
    "Owner": "cruzzi-myth",
    "Repo":  "ghost-devops",
    "Token": ""   // Set via environment variable — never hardcode
  }
}
```
