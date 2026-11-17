"""Notify the configured Discord channel when a stable Cyno release is published."""
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

REPOSITORY = "MekilliDoMaNi/Cyno-DS4-AI"


def build_notice(release):
    if release.get("draft") or release.get("prerelease"):
        return None
    url = release.get("html_url", "")
    if not url.startswith(f"https://github.com/{REPOSITORY}/releases/tag/"):
        raise ValueError("Release non appartenente al repository Cyno.")
    tag = str(release.get("tag_name", ""))
    if not tag or len(tag) > 100:
        raise ValueError("Versione non valida.")
    return {
        "username": "Cyno DS4.AI - Aggiornamenti",
        "allowed_mentions": {"parse": []},
        "embeds": [{
            "title": f"Cyno DS4.AI {tag} disponibile",
            "description": "È disponibile una nuova versione di Cyno DS4.AI.\n"
                "Apri la pagina della versione per scaricare l'app e leggere le novità.\n"
                "Dalla versione 0.6.3 puoi cercare gli aggiornamenti anche nella scheda Sistema dell'app.",
            "url": url,
            "color": 0x32F6FF,
            "footer": {"text": "Cyno DS4.AI - GitHub Releases"},
        }],
    }


def webhook_url(value):
    uri = urllib.parse.urlsplit(value)
    parts = uri.path.strip("/").split("/")
    if (uri.scheme != "https" or uri.hostname not in ("discord.com", "discordapp.com")
            or uri.username or uri.password or uri.query or uri.fragment
            or len(parts) != 4 or parts[:2] != ["api", "webhooks"]
            or not parts[2].isdigit() or not parts[3]):
        raise ValueError("Webhook Discord non valido. Usa l'URL base copiato da Discord, senza /github.")
    return value


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def main():
    # Never print the webhook URL or exception text: the URL contains a private token.
    secret = os.environ.get("DISCORD_WEBHOOK_URL", "").strip()
    if not secret:
        print("Notifiche non attive: configura il repository secret DISCORD_WEBHOOK_URL.", file=sys.stderr)
        return 1
    try:
        url = webhook_url(secret)
        if os.environ.get("GITHUB_REPOSITORY") != REPOSITORY:
            raise ValueError("Repository inatteso.")
        with open(os.environ["GITHUB_EVENT_PATH"], encoding="utf-8") as stream:
            event = json.load(stream)
        if os.environ.get("GITHUB_EVENT_NAME") == "workflow_dispatch":
            request = urllib.request.Request(
                f"https://api.github.com/repos/{REPOSITORY}/releases/latest",
                headers={"User-Agent": "CynoDS4AI-ReleaseNotifier"})
            with urllib.request.urlopen(request, timeout=30) as response:
                release = json.load(response)
        else:
            if event.get("action") != "published":
                print("Evento ignorato: non è una pubblicazione.")
                return 0
            release = event["release"]
        notice = build_notice(release)
        if notice is None:
            print("Bozza o prerelease: nessuna notifica.")
            return 0
        request = urllib.request.Request(url, data=json.dumps(notice).encode("utf-8"),
            headers={"Content-Type": "application/json", "User-Agent": "CynoDS4AI-ReleaseNotifier"}, method="POST")
        with urllib.request.build_opener(NoRedirect()).open(request, timeout=30) as response:
            if response.status not in (200, 204):
                raise ValueError("Risposta inattesa.")
        print("Notifica versione inviata al canale Discord configurato.")
        return 0
    except urllib.error.HTTPError as error:
        print(f"Invio non riuscito: risposta HTTP {error.code}. Controlla webhook e permessi.", file=sys.stderr)
    except Exception:
        print("Invio non riuscito. Controlla webhook privato, evento e connessione.", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())