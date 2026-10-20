package com.mihoyo.hyperion.biz.miniwidget.napavatar.view;

import Ce.P;
import G50.l;
import Xe.E;
import Zy.j;
import android.content.Context;
import android.graphics.Color;
import android.util.AttributeSet;
import android.view.LayoutInflater;
import android.widget.ImageView;
import androidx.constraintlayout.widget.ConstraintLayout;
import com.bumptech.glide.m;
import com.bumptech.glide.n;
import jg.C0631s;
import kotlin.jvm.internal.DefaultConstructorMarker;
import kotlin.jvm.internal.K;
import kotlin.jvm.internal.r0;
import pg.InterfaceC0771a;
import x0.v;

@v(parameters = 0)
@r0({"SMAP\nNapSettingItemView.kt\nKotlin\n*S Kotlin\n*F\n+ 1 NapSettingItemView.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/view/NapSettingItemView\n+ 2 Color.kt\nandroidx/core/graphics/ColorKt\n+ 3 View.kt\nandroidx/core/view/ViewKt\n*L\n1#1,42:1\n470#2:43\n256#3,2:44\n*S KotlinDebug\n*F\n+ 1 NapSettingItemView.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/view/NapSettingItemView\n*L\n31#1:43\n35#1:44,2\n*E\n"})
/* loaded from: X:\AI project\解包\tools\classes12.dex */
public final class NapSettingItemView extends ConstraintLayout {
    public static final int b = 8;

    @l
    public final C0631s a;

    /* JADX WARN: 'this' call moved to the top of the method (can break code semantics) */
    @j
    public NapSettingItemView(@l Context context) {
        this(context, null, 0, 6, null);
        K.p(context, "context");
    }

    public final void A(@l InterfaceC0771a interfaceC0771a) {
        m D;
        K.p(interfaceC0771a, "info");
        this.a.d.setText(interfaceC0771a.d());
        n n = E.a.n(this.a.b);
        if (n != null && (D = n.D(interfaceC0771a.c())) != null) {
            D.l1(this.a.b);
        }
        try {
            this.a.b.setBackgroundColor(Color.parseColor(interfaceC0771a.b()));
        } catch (Exception unused) {
            this.a.b.setBackgroundColor(0);
        }
        ImageView imageView = this.a.e;
        K.o(imageView, "selectIcon");
        imageView.setVisibility(interfaceC0771a.e() ? 0 : 8);
        if (interfaceC0771a.e()) {
            this.a.c.setBackgroundColor(-8704);
        } else {
            this.a.c.setBackgroundColor(-13421260);
        }
    }

    /* JADX WARN: 'this' call moved to the top of the method (can break code semantics) */
    @j
    public NapSettingItemView(@l Context context, @G50.m AttributeSet attributeSet) {
        this(context, attributeSet, 0, 4, null);
        K.p(context, "context");
    }

    public /* synthetic */ NapSettingItemView(Context context, AttributeSet attributeSet, int i, int i2, DefaultConstructorMarker defaultConstructorMarker) {
        this(context, (i2 & 2) != 0 ? null : attributeSet, (i2 & 4) != 0 ? 0 : i);
    }

    /* JADX WARN: 'super' call moved to the top of the method (can break code semantics) */
    /* JADX WARN: Multi-variable type inference failed */
    @j
    public NapSettingItemView(@l Context context, @G50.m AttributeSet attributeSet, int i) {
        super(context, attributeSet, i);
        K.p(context, "context");
        C0631s a = C0631s.a(LayoutInflater.from(context), this);
        K.o(a, "inflate(...)");
        this.a = a;
        setPadding(P.Z(4), P.Z(6), P.Z(4), P.Z(6));
    }
}
