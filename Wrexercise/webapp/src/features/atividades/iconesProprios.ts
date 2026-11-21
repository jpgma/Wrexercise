import { createLucideIcon } from "lucide-react";

/**
 * Ícones que o lucide não tem, desenhados na mesma grade de 24 px e com o mesmo traço, para ficarem
 * iguais aos outros.
 */

/** Uma pessoa correndo, inclinada para a frente: o ícone da Corrida. */
export const PessoaCorrendo = createLucideIcon("pessoa-correndo", [
  ["circle", { cx: "15", cy: "4", r: "2", key: "cabeca" }],
  ["path", { d: "m13 8-3 6", key: "tronco" }],
  ["path", { d: "m13 8 3 3 3-1", key: "braco-frente" }],
  ["path", { d: "m13 8-4 1-2 3", key: "braco-tras" }],
  ["path", { d: "m10 14 4 2-1 5", key: "perna-frente" }],
  ["path", { d: "m10 14-2 4-4 1", key: "perna-tras" }],
]);

/** Uma bola de futebol: o pentágono no meio e as costuras até a borda. */
export const BolaFutebol = createLucideIcon("bola-futebol", [
  ["circle", { cx: "12", cy: "12", r: "10", key: "bola" }],
  ["path", { d: "M12 7.5 16.8 11l-1.9 5.5H9.1L7.2 11Z", key: "pentagono" }],
  ["path", { d: "M12 7.5V2", key: "costura-1" }],
  ["path", { d: "m16.8 11 4.7-2.1", key: "costura-2" }],
  ["path", { d: "m14.9 16.5 3 3.6", key: "costura-3" }],
  ["path", { d: "m9.1 16.5-3 3.6", key: "costura-4" }],
  ["path", { d: "M7.2 11 2.5 8.9", key: "costura-5" }],
]);
