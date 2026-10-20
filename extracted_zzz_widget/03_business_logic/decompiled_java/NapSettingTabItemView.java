package com.mihoyo.hyperion.biz.miniwidget.napavatar.view;

import G50.l;
import Xe.E;
import Zy.j;
import android.content.Context;
import android.util.AttributeSet;
import android.view.LayoutInflater;
import android.widget.FrameLayout;
import com.bumptech.glide.m;
import com.bumptech.glide.n;
import com.mihoyo.hyperion.biz.miniwidget.napavatar.AvatarBackground;
import jg.C0632t;
import kotlin.jvm.internal.DefaultConstructorMarker;
import kotlin.jvm.internal.K;
import x0.v;

@v(parameters = 0)
/* loaded from: X:\AI project\解包\tools\classes12.dex */
public final class NapSettingTabItemView extends FrameLayout {
    public static final int b = 8;

    @l
    public final C0632t a;

    /* JADX WARN: 'this' call moved to the top of the method (can break code semantics) */
    @j
    public NapSettingTabItemView(@l Context context) {
        this(context, null, 0, 6, null);
        K.p(context, "context");
    }

    public final void a(@l AvatarBackground.b bVar) {
        K.p(bVar, "type");
        n n = E.a.n(this.a.b);
        if (n != null) {
            m E = n.E(bVar.i() ? bVar.h() : bVar.f());
            if (E != null) {
                E.l1(this.a.b);
            }
        }
    }

    /* JADX WARN: 'this' call moved to the top of the method (can break code semantics) */
    @j
    public NapSettingTabItemView(@l Context context, @G50.m AttributeSet attributeSet) {
        this(context, attributeSet, 0, 4, null);
        K.p(context, "context");
    }

    public /* synthetic */ NapSettingTabItemView(Context context, AttributeSet attributeSet, int i, int i2, DefaultConstructorMarker defaultConstructorMarker) {
        this(context, (i2 & 2) != 0 ? null : attributeSet, (i2 & 4) != 0 ? 0 : i);
    }

    /* JADX WARN: 'super' call moved to the top of the method (can break code semantics) */
    @j
    public NapSettingTabItemView(@l Context context, @G50.m AttributeSet attributeSet, int i) {
        super(context, attributeSet, i);
        K.p(context, "context");
        C0632t a = C0632t.a(LayoutInflater.from(context), this);
        K.o(a, "inflate(...)");
        this.a = a;
    }
}
