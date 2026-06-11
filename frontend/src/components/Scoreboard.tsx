import "./Scoreboard.css";
import type { ScoredResult } from "../types/game";
import { choiceIcon } from "../constants/game";

interface Props {
  scoreboard: ScoredResult[];
  disabled: boolean;
  onReset: () => void;
  choiceMap: Record<string, string>;
}

export function Scoreboard({ scoreboard, disabled, onReset, choiceMap }: Readonly<Props>) {
  return (
    <div className="scoreboard">
      <div className="scoreboard-header">
        <h3>Last 10 Results</h3>
        <button
          className="reset-btn"
          onClick={onReset}
          disabled={disabled || scoreboard.length === 0}
        >
          Reset
        </button>
      </div>
      {scoreboard.length === 0 ? (
        <p className="empty">No games played yet.</p>
      ) : (
        <ul>
          {scoreboard.map((r) => (
            <li key={r.id} className={`score-item score-item--${r.results}`}>
              <span>{r.results.toUpperCase()}</span>
              <span>
                {choiceIcon(choiceMap[String(r.player)])} vs {choiceIcon(choiceMap[String(r.computer)])}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
