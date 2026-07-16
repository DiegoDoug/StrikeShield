import type { ReactNode } from 'react'

export interface DagNode {
  key: string
  dependsOn: string[]
}

/** Topologically layers nodes for a left-to-right DAG render: level = 1 + max(level of dependencies), 0 if none. */
export function computeDagLevels<T extends DagNode>(nodes: T[]): T[][] {
  const byKey = new Map(nodes.map((n) => [n.key, n]))
  const levelByKey = new Map<string, number>()

  const resolve = (key: string, guard: Set<string> = new Set()): number => {
    if (levelByKey.has(key)) return levelByKey.get(key)!
    if (guard.has(key)) return 0 // cyclical/misconfigured — don't infinite-loop, just render at level 0
    guard.add(key)
    const node = byKey.get(key)
    const deps = node?.dependsOn.filter((d) => byKey.has(d)) ?? []
    const level = deps.length === 0 ? 0 : 1 + Math.max(...deps.map((d) => resolve(d, guard)))
    levelByKey.set(key, level)
    return level
  }

  for (const node of nodes) resolve(node.key)

  const maxLevel = Math.max(0, ...Array.from(levelByKey.values()))
  const levels: T[][] = Array.from({ length: maxLevel + 1 }, () => [])
  for (const node of nodes) {
    levels[levelByKey.get(node.key)!].push(node)
  }
  return levels
}

export function DagView<T extends DagNode>({
  nodes,
  renderNode,
}: {
  nodes: T[]
  renderNode: (node: T) => ReactNode
}) {
  const levels = computeDagLevels(nodes)

  return (
    <div className="flex items-start gap-8 overflow-x-auto pb-2">
      {levels.map((level, i) => (
        <div key={i} className="flex shrink-0 flex-col gap-3">
          {level.map((node) => (
            <div key={node.key} className="relative">
              {renderNode(node)}
              {node.dependsOn.length > 0 && (
                <p className="mt-1 flex flex-wrap items-center gap-1 text-[0.65rem] text-tertiary">
                  <span aria-hidden>←</span>
                  {node.dependsOn.join(', ')}
                </p>
              )}
            </div>
          ))}
        </div>
      ))}
    </div>
  )
}
