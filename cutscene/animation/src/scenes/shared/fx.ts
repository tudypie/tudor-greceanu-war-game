import {Layout, Node} from '@motion-canvas/2d';
import {Reference, ThreadGenerator} from '@motion-canvas/core';

export function* shake(
  node: Reference<Node> | Reference<Layout>,
  frames: number,
  magnitude: number,
): ThreadGenerator {
  for (let i = 0; i < frames; i++) {
    node().position([
      Math.random() * magnitude * 2 - magnitude,
      Math.random() * magnitude * 2 - magnitude,
    ]);
    yield;
  }
  node().position([0, 0]);
}

export function* glitch(
  node: Reference<Node> | Reference<Layout>,
  flashes: number = 3,
): ThreadGenerator {
  for (let i = 0; i < flashes; i++) {
    node().position([Math.random() * 24 - 12, Math.random() * 8 - 4]);
    node().opacity(0.4);
    yield;
    node().opacity(1);
    yield;
  }
  node().position([0, 0]);
  node().opacity(1);
}

