import { useEffect, useMemo, useRef, useState } from "react";
import { api } from "../api/gameApi";
import type { Choice, GameStatus, ScoredResult } from "../types/game";

export function useGame() {
  const [choices, setChoices] = useState<Choice[]>([]);
  const [result, setResult] = useState<ScoredResult | null>(null);
  const [scoreboard, setScoreboard] = useState<ScoredResult[]>([]);
  const [status, setStatus] = useState<GameStatus>("initializing");
  const [error, setError] = useState<string | null>(null);

  // Holds the AbortController for the in-flight play/reset request so a
  // subsequent call can cancel the previous one before firing a new request.
  const abortRef = useRef<AbortController | null>(null);

  const choiceMap = useMemo(
    () => Object.fromEntries(choices.map((c) => [String(c.id), c.name])),
    [choices]
  );

  useEffect(() => {
    const controller = new AbortController();

    Promise.all([
      api.getChoices(controller.signal),
      api.getScoreboard(controller.signal),
    ])
      .then(([c, s]) => {
        setChoices(c);
        setScoreboard(s.map((r) => ({ ...r, id: crypto.randomUUID() })));
      })
      .catch((e: unknown) => {
        if (e instanceof Error && e.name === "AbortError") return;
        setError("Could not connect to the game server. Contact the support team.");
      })
      .finally(() => setStatus("idle"));

    return () => controller.abort();
  }, []);

  const play = async (choiceId: number) => {
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    setStatus("busy");
    setError(null);
    try {
      const r = await api.play(choiceId, controller.signal);
      const scored: ScoredResult = { ...r, id: crypto.randomUUID() };
      setResult(scored);
      setScoreboard((prev) => [scored, ...prev].slice(0, 10));
    } catch (e) {
      if (e instanceof Error && e.name === "AbortError") return;
      setError(e instanceof Error ? e.message : "Something went wrong. Please try again.");
    } finally {
      setStatus("idle");
    }
  };

  const reset = async () => {
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    setStatus("busy");
    setError(null);
    try {
      await api.resetScoreboard(controller.signal);
      setScoreboard([]);
      setResult(null);
    } catch (e) {
      if (e instanceof Error && e.name === "AbortError") return;
      setError("Failed to reset scoreboard.");
    } finally {
      setStatus("idle");
    }
  };

  return { choices, result, scoreboard, status, error, choiceMap, play, reset };
}
