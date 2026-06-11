import "./ResultCard.css";
import type { ScoredResult } from "../types/game";
import { RESULT_LABEL, choiceIcon } from "../constants/game";

interface Props {
  result: ScoredResult;
  choiceMap: Record<string, string>;
}

export function ResultCard({ result, choiceMap }: Readonly<Props>) {
  const playerName = choiceMap[String(result.player)];
  const computerName = choiceMap[String(result.computer)];

  return (
    <div className={`result result--${result.results}`}>
      <h2>{RESULT_LABEL[result.results]}</h2>
      <p>
        You chose{" "}
        <strong className="choice-label">
          <span>{choiceIcon(playerName)}</span>
          <span>{playerName}</span>
        </strong>{" "}
        — Computer chose{" "}
        <strong className="choice-label">
          <span>{choiceIcon(computerName)}</span>
          <span>{computerName}</span>
        </strong>
      </p>
    </div>
  );
}
