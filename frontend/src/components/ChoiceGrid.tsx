import "./ChoiceGrid.css";
import type { Choice } from "../types/game";
import { choiceIcon } from "../constants/game";

interface Props {
  choices: Choice[];
  disabled: boolean;
  onSelect: (id: number) => void;
}

export function ChoiceGrid({ choices, disabled, onSelect }: Readonly<Props>) {
  return (
    <div className="choices">
      {choices.map((c) => (
        <button
          key={c.id}
          className="choice-btn"
          onClick={() => onSelect(c.id)}
          disabled={disabled}
        >
          <span className="icon">{choiceIcon(c.name)}</span>
          <span className="label">{c.name}</span>
        </button>
      ))}
    </div>
  );
}
