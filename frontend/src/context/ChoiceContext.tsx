import { createContext, useContext } from "react";
import type { ReactNode } from "react";

const ChoiceContext = createContext<Record<string, string>>({});

interface Props {
  choiceMap: Record<string, string>;
  children: ReactNode;
}

export function ChoiceProvider({ choiceMap, children }: Readonly<Props>) {
  return (
    <ChoiceContext.Provider value={choiceMap}>
      {children}
    </ChoiceContext.Provider>
  );
}

export function useChoiceMap(): Record<string, string> {
  return useContext(ChoiceContext);
}
