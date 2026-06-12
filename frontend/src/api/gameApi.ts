import type { Choice, PlayResult } from "../types/game";

const BASE_URL = window.__ENV__?.API_URL || import.meta.env.VITE_API_URL || "http://localhost:5000";

function getPlayerId(): string {
  const key = "bazinga_player_id";
  let id = localStorage.getItem(key);
  if (!id) {
    id = crypto.randomUUID();
    localStorage.setItem(key, id);
  }
  return id;
}

async function request<T>(url: string, options?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...options,
    headers: {
      "X-Player-Id": getPlayerId(),
      ...options?.headers,
    },
  });

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.error ?? `Request failed (${response.status})`);
  }
  if (response.status === 204) return undefined as T;
  return response.json();
}

export const api = {
  getChoices: (signal?: AbortSignal): Promise<Choice[]> =>
    request<Choice[]>(`${BASE_URL}/choices`, { signal }),

  play: (player: number, signal?: AbortSignal): Promise<PlayResult> =>
    request<PlayResult>(`${BASE_URL}/play`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ player }),
      signal,
    }),

  getScoreboard: (signal?: AbortSignal): Promise<PlayResult[]> =>
    request<PlayResult[]>(`${BASE_URL}/scoreboard`, { signal }),

  resetScoreboard: (signal?: AbortSignal): Promise<void> =>
    request<void>(`${BASE_URL}/scoreboard`, { method: "DELETE", signal }),
};
