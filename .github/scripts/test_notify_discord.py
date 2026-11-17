import contextlib
import importlib.util
import io
import os
from pathlib import Path
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("notify_discord", Path(__file__).with_name("notify_discord.py"))
notify = importlib.util.module_from_spec(spec)
spec.loader.exec_module(notify)

class NotificationTests(unittest.TestCase):
    def release(self, **changes):
        return dict(html_url="https://github.com/MekilliDoMaNi/Cyno-DS4-AI/releases/tag/v0.6.3", tag_name="v0.6.3", draft=False, prerelease=False, **changes)
    def test_release_has_download_link_and_no_mentions(self):
        notice=notify.build_notice(self.release())
        self.assertEqual(notice["allowed_mentions"], {"parse": []})
        self.assertIn("v0.6.3", notice["embeds"][0]["title"])
        self.assertEqual(notice["embeds"][0]["url"], self.release()["html_url"])
    def test_drafts_and_prereleases_are_not_sent(self):
        for field in ("draft", "prerelease"):
            release=self.release();release[field]=True
            self.assertIsNone(notify.build_notice(release))
    def test_other_repository_is_rejected(self):
        release=self.release();release["html_url"]="https://evil.example/releases/tag/v1"
        with self.assertRaises(ValueError): notify.build_notice(release)
    def test_only_direct_discord_webhook_is_accepted(self):
        good="https://discord.com/api/webhooks/123/token"
        self.assertEqual(notify.webhook_url(good),good)
        for url in ("http://discord.com/api/webhooks/123/token", "https://evil.example/api/webhooks/123/token", good+"/github", good+"?x=y"):
            with self.assertRaises(ValueError): notify.webhook_url(url)
    def test_secret_is_not_printed_on_failure(self):
        output=io.StringIO()
        with patch.dict(os.environ,{"DISCORD_WEBHOOK_URL":"https://evil.example/PRIVATE_TOKEN"},clear=True), contextlib.redirect_stderr(output):
            self.assertEqual(notify.main(),1)
        self.assertNotIn("PRIVATE_TOKEN",output.getvalue())
    def test_missing_secret_does_not_send(self):
        with patch.dict(os.environ,{},clear=True),contextlib.redirect_stderr(io.StringIO()),patch("urllib.request.urlopen") as request:
            self.assertEqual(notify.main(),1);request.assert_not_called()

if __name__ == "__main__": unittest.main()