import SwiftUI
import AppKit

// MARK: - Converze Radial Wheel Metrics & Geometry

public struct ConverzeWheelMetrics: Sendable {
    /// Scale factor for native macOS desktop wheel (1.12x -> 304.6pt diameter)
    public let scale: CGFloat

    public init(scale: CGFloat = 1.12) {
        self.scale = scale
    }

    public var innerRadius: CGFloat { 48.64 * scale }
    public var outerRadius: CGFloat { 119.52 * scale }
    public var backdropRadius: CGFloat { 125.92 * scale }
    public var holeRadius: CGFloat { 42.24 * scale }
    public var gap: CGFloat { 4.0 * scale }
    public var corner: CGFloat { 20.16 * scale }
    public var labelInner: CGFloat { 40.0 * scale }
    public var diameter: CGFloat { 272.0 * scale }

    // MARK: - Polar Helpers

    public static func centerAngle(index: Int, count: Int) -> CGFloat {
        guard count > 0 else { return .pi / 2 }
        let start: CGFloat = (count == 3) ? .pi : (.pi / 2)
        return start - (.pi * 2.0 * CGFloat(index) / CGFloat(count))
    }

    public static func polarOffset(radius: CGFloat, angle: CGFloat) -> CGPoint {
        CGPoint(x: cos(angle) * radius, y: -sin(angle) * radius)
    }

    public static func polarPoint(center: CGPoint, radius: CGFloat, angle: CGFloat) -> CGPoint {
        CGPoint(x: center.x + cos(angle) * radius, y: center.y - sin(angle) * radius)
    }

    /// Exact geometric midpoint between innerRadius and outerRadius along the slice centerline
    public func labelCenterOffset(index: Int, count: Int) -> CGPoint {
        let r = (innerRadius + outerRadius) * 0.5
        let theta = Self.centerAngle(index: index, count: count)
        return Self.polarOffset(radius: r, angle: theta)
    }

    /// Polar hit-test bounded to the wheel ring so clicking outside the wheel is detected cleanly
    public func hitTestSlice(dx: CGFloat, dy: CGFloat, count: Int) -> Int? {
        guard count > 0 else { return nil }
        let dist = hypot(dx, dy)
        guard dist >= (holeRadius * 0.75) && dist <= (backdropRadius * 1.12) else { return nil }

        let cursorAngle = atan2(-dy, dx)
        let step = (.pi * 2.0) / CGFloat(count)
        let firstAngle = Self.centerAngle(index: 0, count: count)

        let rawDelta = (firstAngle - cursorAngle).truncatingRemainder(dividingBy: .pi * 2.0)
        let normalizedDelta = (rawDelta + .pi * 2.0).truncatingRemainder(dividingBy: .pi * 2.0)
        let candidate = Int(round(normalizedDelta / step)) % count
        return candidate
    }
}

// MARK: - Converze Rounded Wedge Shape

public struct ConverzePetalShape: Shape, Sendable {
    public let index: Int
    public let count: Int
    public let metrics: ConverzeWheelMetrics

    public init(index: Int, count: Int, metrics: ConverzeWheelMetrics = ConverzeWheelMetrics()) {
        self.index = index
        self.count = count
        self.metrics = metrics
    }

    public func path(in rect: CGRect) -> Path {
        var path = Path()
        guard count > 0 else { return path }

        let center = CGPoint(x: rect.midX, y: rect.midY)
        let inner = metrics.innerRadius
        let outer = metrics.outerRadius
        let gap = metrics.gap
        let corner = metrics.corner

        let a = ConverzeWheelMetrics.centerAngle(index: index, count: count)
        let n = (.pi * 2.0) / CGFloat(count)
        let l = a - n / 2.0
        let s = a + n / 2.0

        func halfGapAngle(_ radius: CGFloat) -> CGFloat {
            let ratio = min(0.95, (gap / 2.0) / max(radius, gap / 2.0 + 0.01))
            return asin(ratio)
        }

        let d = l + halfGapAngle(outer)
        let i = s - halfGapAngle(outer)
        let c = l + halfGapAngle(inner)
        let g = s - halfGapAngle(inner)

        let f = min(0.15, (i - d) * 0.24)
        let u = min(0.18, (g - c) * 0.18)

        let v = inner / max(0.01, cos(u))
        let y = v + inner * tan(u)
        let O = outer - corner

        func pt(_ r: CGFloat, _ theta: CGFloat) -> CGPoint {
            ConverzeWheelMetrics.polarPoint(center: center, radius: r, angle: theta)
        }

        let startPt = pt(y, l + halfGapAngle(y))
        path.move(to: startPt)
        path.addLine(to: pt(O, l + halfGapAngle(O)))
        path.addQuadCurve(to: pt(outer, d + f), control: pt(outer, d))

        let outerArcSteps = 28
        let outerStart = d + f
        let outerEnd = i - f
        if outerEnd > outerStart {
            for stepIdx in 1...outerArcSteps {
                let t = CGFloat(stepIdx) / CGFloat(outerArcSteps)
                let theta = outerStart + (outerEnd - outerStart) * t
                path.addLine(to: pt(outer, theta))
            }
        }

        path.addQuadCurve(to: pt(O, s - halfGapAngle(O)), control: pt(outer, i))
        path.addLine(to: pt(y, s - halfGapAngle(y)))
        path.addQuadCurve(to: pt(inner, g - u), control: pt(v, s - halfGapAngle(v)))

        let innerArcSteps = 20
        let innerStart = g - u
        let innerEnd = c + u
        if innerStart > innerEnd {
            for stepIdx in 1...innerArcSteps {
                let t = CGFloat(stepIdx) / CGFloat(innerArcSteps)
                let theta = innerStart - (innerStart - innerEnd) * t
                path.addLine(to: pt(inner, theta))
            }
        }

        path.addQuadCurve(to: startPt, control: pt(v, l + halfGapAngle(v)))
        path.closeSubpath()
        return path
    }
}

// MARK: - Frosted Glass Donut Backdrop Shape (With Circular Cutout in Center)

public struct DonutRingShape: Shape, Sendable {
    public let innerRadius: CGFloat
    public let outerRadius: CGFloat

    public init(innerRadius: CGFloat, outerRadius: CGFloat) {
        self.innerRadius = innerRadius
        self.outerRadius = outerRadius
    }

    public func path(in rect: CGRect) -> Path {
        let center = CGPoint(x: rect.midX, y: rect.midY)
        var path = Path()
        path.addEllipse(in: CGRect(
            x: center.x - outerRadius,
            y: center.y - outerRadius,
            width: outerRadius * 2.0,
            height: outerRadius * 2.0
        ))
        path.addEllipse(in: CGRect(
            x: center.x - innerRadius,
            y: center.y - innerRadius,
            width: innerRadius * 2.0,
            height: innerRadius * 2.0
        ))
        return path
    }
}
