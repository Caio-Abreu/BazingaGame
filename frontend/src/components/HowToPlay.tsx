import { useEffect, useRef } from "react";
import "./HowToPlay.css";

const RULES = [
  { winner: "Scissors", loser: "Paper", verb: "cuts" },
  { winner: "Paper", loser: "Rock", verb: "covers" },
  { winner: "Rock", loser: "Lizard", verb: "crushes" },
  { winner: "Lizard", loser: "Spock", verb: "poisons" },
  { winner: "Spock", loser: "Scissors", verb: "smashes" },
  { winner: "Scissors", loser: "Lizard", verb: "decapitates" },
  { winner: "Lizard", loser: "Paper", verb: "eats" },
  { winner: "Paper", loser: "Spock", verb: "disproves" },
  { winner: "Spock", loser: "Rock", verb: "vaporizes" },
  { winner: "Rock", loser: "Scissors", verb: "crushes" },
];

interface Props {
  onClose: () => void;
}

export function HowToPlay({ onClose }: Readonly<Props>) {
  const dialogRef = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    dialogRef.current?.showModal();
  }, []);

  const handleBackdropClick = (e: React.MouseEvent<HTMLDialogElement>) => {
    if (e.target === dialogRef.current) onClose();
  };

  return (
    <dialog
      ref={dialogRef}
      className="how-to-play"
      aria-label="How to play"
      onClose={onClose}
      onClick={handleBackdropClick}
    >
      <div className="how-to-play__content">
        <div className="how-to-play__header">
          <h2>How to play</h2>
          <button className="how-to-play__close" onClick={onClose} aria-label="Close">
            ✕
          </button>
        </div>
        <p className="how-to-play__intro">
          Pick one of the five choices. The computer picks randomly. First to beat the computer wins!
        </p>
        <ul className="how-to-play__rules">
          {RULES.map(({ winner, loser, verb }) => (
            <li key={`${winner}-${loser}`}>
              <strong>{winner}</strong> {verb} <strong>{loser}</strong>
            </li>
          ))}
        </ul>
      </div>
    </dialog>
  );
}
