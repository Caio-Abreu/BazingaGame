import type { PlayResult } from "../types/game";

export const ICONS: Record<string, string> = {
  rock: "✊",
  paper: "✋",
  scissors: "✌️",
  lizard: "🦎",
  spock: "🖖",
};

export const RESULT_LABEL: Record<PlayResult["results"], string> = {
  win: "You Win!",
  lose: "You Lose!",
  tie: "It's a Tie!",
};

export const ICON_FALLBACK = "❓";

export function choiceIcon(name: string | undefined): string {
  return (name !== undefined && name !== "" && ICONS[name]) || ICON_FALLBACK;
}
