import { useEffect, useMemo, useState } from "react";
import { api } from "../api/gameApi";
import type { Choice, GameStatus, ScoredResult } from "../types/game";

export function useGame() {
  const [choices, setChoices] = useState<Choice[]>([]);
  const [result, setResult] = useState<ScoredResult | null>(null);
  const [scoreboard, setScoreboard] = useState<ScoredResult[]>([]);
  const [status, setStatus] = useState<GameStatus>("initializing");
  const [error, setError] = useState<string | null>(null);

  const choiceMap = useMemo(
    () => Object.fromEntries(choices.map((c) => [String(c.id), c.name])),
    [choices]
  );

  useEffect(() => {
    Promise.all([api.getChoices(), api.getScoreboard()])
      .then(([c, s]) => {
        setChoices(c);
        setScoreboard(s.map((r) => ({ ...r, id: crypto.randomUUID() })));
      })
      .catch(() => setError("Could not connect to the game server. Is the API running?"))
      .finally(() => setStatus("idle"));
  }, []);

  const play = async (choiceId: number) => {
    setStatus("busy");
    setError(null);
    try {
      const r = await api.play(choiceId);
      const scored: ScoredResult = { ...r, id: crypto.randomUUID() };
      setResult(scored);
      setScoreboard((prev) => [scored, ...prev].slice(0, 10));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Something went wrong. Please try again.");
    } finally {
      setStatus("idle");
    }
  };

  const reset = async () => {
    setStatus("busy");
    setError(null);
    try {
      await api.resetScoreboard();
      setScoreboard([]);
      setResult(null);
    } catch {
      setError("Failed to reset scoreboard.");
    } finally {
      setStatus("idle");
    }
  };

  return { choices, result, scoreboard, status, error, choiceMap, play, reset };
}
