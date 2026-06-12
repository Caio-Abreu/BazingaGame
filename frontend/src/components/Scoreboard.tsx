import "./Scoreboard.css";
import type { ScoredResult } from "../types/game";
import { choiceIcon } from "../constants/game";
import { useChoiceMap } from "../context/ChoiceContext";

interface Props {
  scoreboard: ScoredResult[];
  disabled: boolean;
  onReset: () => void;
}

export function Scoreboard({ scoreboard, disabled, onReset }: Readonly<Props>) {
  const choiceMap = useChoiceMap();

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
        <ul aria-label="Last 10 results">
          {scoreboard.map((r) => (
            <li key={r.id} className={`score-item score-item--${r.results}`}>
              <span>{r.results.toUpperCase()}</span>
              <span aria-label={`${choiceMap[String(r.player)]} vs ${choiceMap[String(r.computer)]}`}>
                <span aria-hidden="true">{choiceIcon(choiceMap[String(r.player)])} vs {choiceIcon(choiceMap[String(r.computer)])}</span>
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
