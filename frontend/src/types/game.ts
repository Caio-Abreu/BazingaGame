export interface Choice {
  id: number;
  name: string;
}

export interface PlayResult {
  results: "win" | "lose" | "tie";
  player: number;
  computer: number;
}

export type ScoredResult = PlayResult & { id: string };

export type GameStatus = "initializing" | "idle" | "busy";
