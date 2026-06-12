import "./ResultCard.css";
import type { ScoredResult } from "../types/game";
import { RESULT_LABEL, choiceIcon } from "../constants/game";
import { useChoiceMap } from "../context/ChoiceContext";

interface Props {
  result: ScoredResult;
}

export function ResultCard({ result }: Readonly<Props>) {
  const choiceMap = useChoiceMap();
  const playerName = choiceMap[String(result.player)];
  const computerName = choiceMap[String(result.computer)];

  return (
    <div className={`result result--${result.results}`} aria-live="polite" aria-atomic="true">
      <h2>{RESULT_LABEL[result.results]}</h2>
      <p>
        You chose{" "}
        <strong className="choice-label">
          <span aria-hidden="true">{choiceIcon(playerName)}</span>
          <span>{playerName}</span>
        </strong>{" "}
        — Computer chose{" "}
        <strong className="choice-label">
          <span aria-hidden="true">{choiceIcon(computerName)}</span>
          <span>{computerName}</span>
        </strong>
      </p>
    </div>
  );
}
